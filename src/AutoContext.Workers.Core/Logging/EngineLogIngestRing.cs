namespace AutoContext.Workers.Core.Logging;

using AutoContext.Engine.Protocol.Messages.Logs;

/// <summary>
/// Worker-side bounded, drop-oldest buffer in front of
/// <see cref="EngineWriteLogClient"/>. Callers hand records to
/// <see cref="Post"/> without ever blocking; a single background
/// drain task ships them to the engine in order, retrying with
/// exponential backoff while the engine is unreachable and replaying
/// the buffer once it reconnects.
/// </summary>
/// <remarks>
/// <para>
/// When the buffer is full — its record count reaches
/// <c>capacity</c> or its estimated size reaches <c>maxBytes</c> —
/// the oldest records are dropped to make room for the newest, so a
/// burst that outruns delivery loses history rather than blocking the
/// worker. Each drop batch is announced once on stderr
/// (<c>engine log dropped N records</c>), and the next record the
/// drain successfully delivers is preceded by a synthetic
/// <c>warning</c> record so the drop is visible in the engine's log
/// too.
/// </para>
/// <para>
/// The drain removes a record from the buffer before shipping it and
/// re-adds it at the head if the send fails, so a record is only ever
/// dropped by <see cref="Post"/>'s overflow policy — never lost to a
/// race between a send in flight and a concurrent overflow.
/// </para>
/// <para>
/// The ring never routes its own diagnostics through
/// <see cref="Microsoft.Extensions.Logging.ILogger"/> — it sits
/// underneath that pipeline, so logging a fault here would feed
/// straight back into itself. stderr is its sole out-of-band channel.
/// </para>
/// </remarks>
public sealed class EngineLogIngestRing : IAsyncDisposable
{
    private const int DefaultCapacity = 1000;
    private const long DefaultMaxBytes = 1024 * 1024;
    private const int DrainStopTimeoutMs = 2000;
    private const int InitialBackoffMs = 200;
    private const int MaxBackoffMs = 5000;
    private const int PerRecordOverheadBytes = 64;

    private readonly LinkedList<Buffered> _buffer = new();
    private long _bufferedBytes;
    private readonly int _capacity;
    private readonly EngineWriteLogClient _client;
    private readonly CancellationTokenSource _cts = new();
    private int _disposed;
    private readonly Task _drainTask;
    private long _droppedCount;
    private readonly Lock _gate = new();
    private readonly long _maxBytes;
    private readonly SemaphoreSlim _signal = new(0, 1);
    private readonly TextWriter _standardError;
    private bool _stderrReported;
    private readonly TimeProvider _timeProvider;
    private readonly string _workerId;

    /// <summary>
    /// Creates and starts a new ring. The drain task runs until
    /// <see cref="DisposeAsync"/> is called.
    /// </summary>
    /// <param name="client">The engine client the drain ships records
    /// through. The ring owns its lifetime and disposes it once the
    /// drain has stopped.</param>
    /// <param name="workerId">The worker's stable short identifier,
    /// used to compose the routing category of the synthetic
    /// dropped-records record.</param>
    /// <param name="timeProvider">Clock used to stamp the synthetic
    /// dropped-records record.</param>
    /// <param name="capacity">Maximum buffered record count before
    /// drop-oldest kicks in.</param>
    /// <param name="maxBytes">Maximum estimated buffered size before
    /// drop-oldest kicks in.</param>
    /// <param name="standardError">Sink for drop announcements.
    /// Defaults to <see cref="Console.Error"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="client"/>, <paramref name="workerId"/>, or
    /// <paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="workerId"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="capacity"/> or <paramref name="maxBytes"/> is
    /// not positive.</exception>
    public EngineLogIngestRing(
        EngineWriteLogClient client,
        string workerId,
        TimeProvider timeProvider,
        int capacity = DefaultCapacity,
        long maxBytes = DefaultMaxBytes,
        TextWriter? standardError = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

        _client = client;
        _workerId = workerId;
        _timeProvider = timeProvider;
        _capacity = capacity;
        _maxBytes = maxBytes;
        _standardError = standardError ?? Console.Error;
        _drainTask = Task.Run(() => DrainAsync(_cts.Token));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _cts.CancelAsync().ConfigureAwait(false);

        try
        {
            await _drainTask.WaitAsync(TimeSpan.FromMilliseconds(DrainStopTimeoutMs)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Drain didn't observe cancellation in time — abandon it.
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }

        // Disposing the client is safe even if an abandoned drain is
        // still mid-send: the client guards its own disposed state and
        // surfaces the torn write as a handled failure.
        await _client.DisposeAsync().ConfigureAwait(false);

        // Only tear down the wait primitives once the drain has actually
        // stopped. Had the bounded wait above timed out, the drain could
        // still be parked in _signal.WaitAsync — disposing it from under
        // that would fault the abandoned task with an unobserved
        // ObjectDisposedException.
        if (_drainTask.IsCompleted)
        {
            _signal.Dispose();
            _cts.Dispose();
        }
    }

    /// <summary>
    /// Buffers <paramref name="record"/> for off-thread delivery to
    /// the engine. Never blocks; when the buffer is full the oldest
    /// records are dropped to make room. A no-op after disposal.
    /// </summary>
    /// <param name="record">The record to enqueue. Must not be
    /// <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="record"/> is <see langword="null"/>.</exception>
    public void Post(JsonLogRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        lock (_gate)
        {
            var bytes = EstimateBytes(record);
            _buffer.AddLast(new Buffered(record, bytes));
            _bufferedBytes += bytes;

            while (_buffer.Count > 1
                && (_buffer.Count > _capacity || _bufferedBytes > _maxBytes)
                && _buffer.First is { } oldest)
            {
                _buffer.RemoveFirst();
                _bufferedBytes -= oldest.Value.Bytes;
                _droppedCount++;
            }
        }

        SignalAvailable();
    }

    private static int EstimateBytes(JsonLogRecord record)
        => record.Message.Length + record.Category.Length + PerRecordOverheadBytes;

    private JsonLogRecord BuildDroppedRecord(long dropped)
        => new()
        {
            Timestamp = _timeProvider.GetUtcNow(),
            Category = WorkerLogCategory.Compose(_workerId, "engine.logging"),
            Level = LogLevels.Warning,
            Message = $"dropped {dropped} worker log records",
        };

    private JsonLogRecord? DequeueOldest()
    {
        lock (_gate)
        {
            if (_buffer.First is { } node)
            {
                _buffer.RemoveFirst();
                _bufferedBytes -= node.Value.Bytes;
                return node.Value.Record;
            }

            return null;
        }
    }

    private async Task DrainAsync(CancellationToken cancellationToken)
    {
        var backoffMs = InitialBackoffMs;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await _signal.WaitAsync(cancellationToken).ConfigureAwait(false);

                var reconnecting = false;

                while (!reconnecting)
                {
                    if (!await TryReportDropsAsync(cancellationToken).ConfigureAwait(false))
                    {
                        reconnecting = true;
                        break;
                    }

                    if (DequeueOldest() is not { } record)
                    {
                        break;
                    }

                    if (await _client.TrySendAsync(record, cancellationToken).ConfigureAwait(false))
                    {
                        backoffMs = InitialBackoffMs;
                    }
                    else
                    {
                        Requeue(record);
                        reconnecting = true;
                    }
                }

                if (reconnecting)
                {
                    await Task.Delay(backoffMs, cancellationToken).ConfigureAwait(false);
                    backoffMs = Math.Min(backoffMs * 2, MaxBackoffMs);
                    SignalAvailable();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown — the buffer's tail is intentionally dropped.
        }
    }

    private void Requeue(JsonLogRecord record)
    {
        lock (_gate)
        {
            var bytes = EstimateBytes(record);
            _buffer.AddFirst(new Buffered(record, bytes));
            _bufferedBytes += bytes;
        }
    }

    private void SignalAvailable()
    {
        try
        {
            _signal.Release();
        }
        catch (SemaphoreFullException)
        {
            // Already signalled — the drain drains everything buffered
            // when it wakes, so one pending token suffices.
        }
        catch (ObjectDisposedException)
        {
            // Raced with disposal — the drain is stopping anyway.
        }
    }

    private async Task<bool> TryReportDropsAsync(CancellationToken cancellationToken)
    {
        long dropped;
        bool announceOnStderr;

        lock (_gate)
        {
            dropped = _droppedCount;

            if (dropped == 0)
            {
                return true;
            }

            announceOnStderr = !_stderrReported;
            _stderrReported = true;
        }

        if (announceOnStderr)
        {
            await _standardError.WriteLineAsync($"engine log dropped {dropped} records").ConfigureAwait(false);
        }

        var notice = BuildDroppedRecord(dropped);

        if (!await _client.TrySendAsync(notice, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        lock (_gate)
        {
            _droppedCount -= dropped;

            if (_droppedCount == 0)
            {
                _stderrReported = false;
            }
        }

        return true;
    }

    private readonly record struct Buffered(JsonLogRecord Record, int Bytes);
}
