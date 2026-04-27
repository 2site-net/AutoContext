namespace AutoContext.Worker.Logging;

using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

using AutoContext.Worker.Hosting;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

/// <summary>
/// Background pipe-client that drains worker <see cref="LogRecord"/> values
/// from a bounded channel and writes them as NDJSON over a named pipe to
/// the extension-side LogServer. When the pipe is unavailable (no
/// <c>--log-pipe</c> argument, the connect attempt fails, or the pipe
/// subsequently breaks) the client transparently falls back to writing
/// each record to stderr in a human-readable single-line format.
/// </summary>
/// <remarks>
/// The bounded channel uses <see cref="BoundedChannelFullMode.DropOldest"/>
/// — log spam can never block worker code or grow memory unbounded. A
/// single drain task owns all I/O. Failures (broken pipe, serialisation
/// errors) are intentionally swallowed: this type IS the logger of last
/// resort, so it must never throw.
/// </remarks>
internal sealed class LogServerClient : IAsyncDisposable
{
    private const int QueueCapacity = 1024;
    private const int ConnectTimeoutMs = 2000;

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly Channel<LogRecord> _queue = Channel.CreateBounded<LogRecord>(
        new BoundedChannelOptions(QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

    private readonly string _pipeName;
    private readonly string _clientName;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _drainTask;

    public LogServerClient(IOptions<WorkerHostOptions> options, IHostEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(env);

        _pipeName = options.Value.LogPipe;
        _clientName = env.ApplicationName;
        _drainTask = Task.Run(() => DrainAsync(_cts.Token));
    }

    /// <summary>
    /// Enqueues <paramref name="record"/> for off-thread delivery. Never
    /// blocks; if the queue is full the oldest record is dropped.
    /// </summary>
    public void Enqueue(LogRecord record) => _queue.Writer.TryWrite(record);

    public async ValueTask DisposeAsync()
    {
        _queue.Writer.TryComplete();

        try
        {
            await _drainTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // Drain didn't finish in time — abandon it.
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }

        await _cts.CancelAsync().ConfigureAwait(false);

        // Only dispose the CTS if the drain task has actually exited.
        // Disposing while the task is still mid-await would surface as an
        // ObjectDisposedException on the token — not caught by the drain's
        // OperationCanceledException handler, leading to an unobserved
        // task exception. The CTS is one-per-process; letting the GC
        // reclaim it is harmless.
        if (_drainTask.IsCompleted)
        {
            _cts.Dispose();
        }
    }

    private async Task DrainAsync(CancellationToken ct)
    {
        NamedPipeClientStream? stream = await TryConnectAsync(ct).ConfigureAwait(false);

        try
        {
            if (stream is not null && !await TrySendGreetingAsync(stream, ct).ConfigureAwait(false))
            {
                await stream.DisposeAsync().ConfigureAwait(false);
                stream = null;
            }

            await foreach (var record in _queue.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                if (stream is not null)
                {
                    if (await TryWritePipeAsync(stream, record, ct).ConfigureAwait(false))
                    {
                        continue;
                    }

                    await stream.DisposeAsync().ConfigureAwait(false);
                    stream = null;
                }

                WriteStderr(record);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown — fall through.
        }
        finally
        {
            if (stream is not null)
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task<NamedPipeClientStream?> TryConnectAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_pipeName))
        {
            return null;
        }

        var pipe = new NamedPipeClientStream(
            serverName: ".",
            pipeName: _pipeName,
            direction: PipeDirection.Out,
            options: PipeOptions.Asynchronous);

        try
        {
            await pipe.ConnectAsync(ConnectTimeoutMs, ct).ConfigureAwait(false);
            return pipe;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            return null;
        }
    }

    private async Task<bool> TrySendGreetingAsync(Stream stream, CancellationToken ct)
    {
        try
        {
            var json = JsonSerializer.Serialize(
                new LogGreetingWire(_clientName),
                LogServerJsonContext.Default.LogGreetingWire);
            var bytes = Utf8NoBom.GetBytes(json + "\n");
            await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            return false;
        }
    }

    private static async Task<bool> TryWritePipeAsync(Stream stream, LogRecord record, CancellationToken ct)
    {
        try
        {
            var wire = new LogRecordWire(
                Category: record.Category,
                Level: record.Level.ToString(),
                Message: record.Message,
                Exception: record.Exception?.ToString(),
                CorrelationId: record.CorrelationId);
            var json = JsonSerializer.Serialize(wire, LogServerJsonContext.Default.LogRecordWire);
            var bytes = Utf8NoBom.GetBytes(json + "\n");
            await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            return false;
        }
    }

    private static void WriteStderr(LogRecord record)
    {
        try
        {
            var prefix = record.CorrelationId is null ? string.Empty : $"[{record.CorrelationId}] ";
            var line = $"{prefix}{record.Level}: {record.Category}: {record.Message}";
            Console.Error.WriteLine(line);
            if (record.Exception is not null)
            {
                Console.Error.WriteLine(record.Exception);
            }
        }
        catch (IOException)
        {
            // stderr is gone — nothing we can do.
        }
    }
}
