namespace AutoContext.Framework.Transport;

using System.IO.Pipes;
using System.Threading.Channels;

using Microsoft.Extensions.Logging;

/// <summary>
/// Layer-3 streaming client: drains items from a bounded queue and
/// writes their serialized bytes over a named pipe. Drop-oldest queue
/// semantics keep callers non-blocking. On any I/O failure the stream
/// is closed and remaining items (and any future <see cref="Post"/>
/// items) are routed to <paramref name="fallback"/>.
/// </summary>
/// <remarks>
/// No reconnect policy is built in — matches today's "logger of last
/// resort" behavior in <c>LoggingClient</c>. A dedicated drain task
/// owns all I/O; the type itself is thread-safe for <see cref="Post"/>.
/// Designed to be wrapped by an endpoint class (e.g.
/// <c>LoggingClient</c>) that supplies the <typeparamref name="T"/>
/// type, the serializer, and the fallback path.
/// </remarks>
public sealed partial class PipeStreamingClient<T> : IAsyncDisposable
{
    private const int DefaultDrainTimeoutMs = 2000;

    private readonly PipeTransport _transport;
    private readonly string _pipeName;
    private readonly PipeDirection _direction;
    private readonly int _connectTimeoutMs;
    private readonly ReadOnlyMemory<byte> _greeting;
    private readonly Func<T, ReadOnlyMemory<byte>> _serialize;
    private readonly Action<T>? _fallback;
    private readonly ILogger<PipeStreamingClient<T>> _logger;
    private readonly Channel<T> _queue;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _drainTask;
    private int _disposed;

    /// <summary>
    /// Creates and starts a new streaming client. The drain task runs
    /// until <see cref="DisposeAsync"/> is called.
    /// </summary>
    /// <param name="transport">Connect primitive used by the drain task.</param>
    /// <param name="pipeName">Pipe name; pass empty to disable the pipe and
    /// route every <see cref="Post"/> item through <paramref name="fallback"/>.</param>
    /// <param name="serialize">Maps an item to the bytes written to the pipe.
    /// Called on the drain task; should not allocate excessively.</param>
    /// <param name="logger">Required logger for diagnostic output.</param>
    /// <param name="greeting">Optional handshake bytes written immediately
    /// after connect, before any item is drained. Empty means "skip greeting".</param>
    /// <param name="fallback">Optional sink invoked when the pipe is
    /// unavailable or has broken. Called on the drain task.</param>
    /// <param name="direction">Pipe direction; defaults to <see cref="PipeDirection.Out"/>.</param>
    /// <param name="queueCapacity">Maximum buffered items. Drop-oldest when full.</param>
    /// <param name="connectTimeoutMs">Connect timeout in milliseconds.</param>
    public PipeStreamingClient(
        PipeTransport transport,
        string pipeName,
        Func<T, ReadOnlyMemory<byte>> serialize,
        ILogger<PipeStreamingClient<T>> logger,
        ReadOnlyMemory<byte> greeting = default,
        Action<T>? fallback = null,
        PipeDirection direction = PipeDirection.Out,
        int queueCapacity = 1024,
        int connectTimeoutMs = 2000)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(pipeName);
        ArgumentNullException.ThrowIfNull(serialize);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(queueCapacity);

        _transport = transport;
        _pipeName = pipeName;
        _direction = direction;
        _connectTimeoutMs = connectTimeoutMs;
        _greeting = greeting;
        _serialize = serialize;
        _fallback = fallback;
        _logger = logger;
        _queue = Channel.CreateBounded<T>(
            new BoundedChannelOptions(queueCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            });
        _drainTask = Task.Run(() => DrainAsync(_cts.Token));
    }

    /// <summary>
    /// Posts <paramref name="item"/> for off-thread delivery. Never
    /// blocks; if the queue is full the oldest entry is dropped. Returns
    /// <see langword="false"/> if the client has been disposed.
    /// </summary>
    public bool Post(T item) => _queue.Writer.TryWrite(item);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _queue.Writer.TryComplete();

        try
        {
            await _drainTask.WaitAsync(TimeSpan.FromMilliseconds(DefaultDrainTimeoutMs)).ConfigureAwait(false);
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

        if (_drainTask.IsCompleted)
        {
            _cts.Dispose();
        }
    }

    private async Task DrainAsync(CancellationToken cancellationToken)
    {
        Stream? stream = await TryConnectAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (stream is not null && _greeting.Length > 0
                && !await TryWriteAsync(stream, _greeting, cancellationToken).ConfigureAwait(false))
            {
                await stream.DisposeAsync().ConfigureAwait(false);
                stream = null;
            }

            await foreach (var item in _queue.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (stream is not null)
                {
                    var bytes = _serialize(item);
                    if (await TryWriteAsync(stream, bytes, cancellationToken).ConfigureAwait(false))
                    {
                        continue;
                    }

                    LogStreamBroken(_logger, _pipeName);
                    await stream.DisposeAsync().ConfigureAwait(false);
                    stream = null;
                }

                _fallback?.Invoke(item);
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

    private async Task<Stream?> TryConnectAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_pipeName))
        {
            return null;
        }

        try
        {
            return await _transport.ConnectAsync(_pipeName, _connectTimeoutMs, _direction, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            return null;
        }
    }

    private static async Task<bool> TryWriteAsync(Stream stream, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        try
        {
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            return false;
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Streaming pipe '{PipeName}' broke; routing remaining items to fallback.")]
    private static partial void LogStreamBroken(ILogger logger, string pipeName);
}
