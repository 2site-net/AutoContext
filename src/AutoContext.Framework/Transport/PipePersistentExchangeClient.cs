namespace AutoContext.Framework.Transport;

using System.IO.Pipes;

using Microsoft.Extensions.Logging;

/// <summary>
/// Request/response pipe client that opens a single
/// <see cref="NamedPipeClientStream"/> on first use and reuses it for
/// every subsequent <see cref="ExchangeAsync"/> call. Calls are
/// serialized through an internal lock — only one round-trip is in
/// flight at a time. On any wire failure the connection is closed
/// and the next call reconnects.
/// </summary>
/// <remarks>
/// Endpoint classes that need to coalesce concurrent callers (e.g.
/// <c>WorkerControlClient</c>) layer that on top of this primitive.
/// </remarks>
public sealed partial class PipePersistentExchangeClient : IPipeExchangeClient
{
    private readonly PipeTransport _transport;
    private readonly string _pipeName;
    private readonly int _connectTimeoutMs;
    private readonly ILogger<PipePersistentExchangeClient> _logger;
    private readonly SemaphoreSlim _gate = new(initialCount: 1, maxCount: 1);

    private NamedPipeClientStream? _pipe;
    private bool _disposed;

    /// <summary>
    /// Creates a new <see cref="PipePersistentExchangeClient"/>.
    /// </summary>
    /// <param name="transport">Connect primitive used to (re)connect
    /// the underlying pipe.</param>
    /// <param name="pipeName">Pipe name (without the
    /// <c>\\.\pipe\</c> prefix on Windows). Must be non-empty.</param>
    /// <param name="logger">Required logger for diagnostic output.</param>
    /// <param name="connectTimeoutMs">Connect timeout in milliseconds;
    /// pass <c>0</c> or a negative value to wait indefinitely subject
    /// to the per-call cancellation token.</param>
    public PipePersistentExchangeClient(
        PipeTransport transport,
        string pipeName,
        ILogger<PipePersistentExchangeClient> logger,
        int connectTimeoutMs = 0)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentException.ThrowIfNullOrEmpty(pipeName);
        ArgumentNullException.ThrowIfNull(logger);

        _transport = transport;
        _pipeName = pipeName;
        _connectTimeoutMs = connectTimeoutMs;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<byte[]> ExchangeAsync(byte[] request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            try
            {
                var pipe = await GetOrConnectAsync(cancellationToken).ConfigureAwait(false);
                var codec = new LengthPrefixedFrameCodec(pipe);

                await codec.WriteAsync(request, cancellationToken).ConfigureAwait(false);

                var response = await codec.ReadAsync(cancellationToken).ConfigureAwait(false);
                if (response is null)
                {
                    await ClosePipeAsync().ConfigureAwait(false);
                    throw new IOException(
                        $"Pipe '{_pipeName}' closed before sending a response.");
                }

                return response;
            }
            catch (Exception ex) when (ex is IOException or TimeoutException
                or UnauthorizedAccessException or InvalidDataException)
            {
                await ClosePipeAsync().ConfigureAwait(false);
                throw;
            }
            catch (OperationCanceledException)
            {
                await ClosePipeAsync().ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await ClosePipeAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private async Task<Stream> GetOrConnectAsync(CancellationToken ct)
    {
        if (_pipe is { IsConnected: true })
        {
            return _pipe;
        }

        await ClosePipeAsync().ConfigureAwait(false);

        var stream = await _transport.ConnectAsync(
            _pipeName, _connectTimeoutMs, PipeDirection.InOut, ct).ConfigureAwait(false);

        // PipeTransport.ConnectAsync builds a NamedPipeClientStream
        // and returns it as Stream; we hold it as the concrete type
        // so the IsConnected fast-path works on subsequent calls.
        _pipe = (NamedPipeClientStream)stream;
        LogConnected(_logger, _pipeName);
        return _pipe;
    }

    private async Task ClosePipeAsync()
    {
        if (_pipe is null)
        {
            return;
        }

        var pipe = _pipe;
        _pipe = null;
        try
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
        }
        catch (IOException)
        {
            // Already torn down by the OS — nothing to do.
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Persistent pipe connected to '{PipeName}'.")]
    private static partial void LogConnected(ILogger logger, string pipeName);
}
