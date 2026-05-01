namespace AutoContext.Framework.Transport;

using System.IO.Pipes;

using Microsoft.Extensions.Logging;

/// <summary>
/// Layer-3 keep-alive client: connects, writes a tiny handshake once,
/// and holds the socket open as a process liveness signal. The remote
/// peer treats the live socket as "this host is running" and observes
/// the close when the host exits.
/// </summary>
/// <remarks>
/// When the pipe name is empty (standalone runs, no parent listening)
/// <see cref="StartAsync"/> is a no-op so call sites don't need to
/// special-case standalone scenarios. Connection failures are
/// best-effort — they're logged at <c>Warning</c> and swallowed.
/// </remarks>
public sealed partial class PipeKeepAliveClient : IAsyncDisposable
{
    private readonly PipeTransport _transport;
    private readonly ILogger<PipeKeepAliveClient> _logger;
    private Stream? _stream;
    private bool _disposed;

    /// <summary>
    /// Creates a new <see cref="PipeKeepAliveClient"/>. Both arguments
    /// are required.
    /// </summary>
    public PipeKeepAliveClient(PipeTransport transport, ILogger<PipeKeepAliveClient> logger)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(logger);

        _transport = transport;
        _logger = logger;
    }

    /// <summary>
    /// Connects to <paramref name="pipeName"/> and writes
    /// <paramref name="handshake"/> as a single best-effort attempt.
    /// On success the socket is held until <see cref="DisposeAsync"/>;
    /// on failure the warning is logged and the call returns normally.
    /// </summary>
    public async Task StartAsync(
        string pipeName,
        ReadOnlyMemory<byte> handshake,
        int connectTimeoutMs = 2000,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pipeName);

        if (_disposed)
        {
            return;
        }

        if (pipeName.Length == 0)
        {
            LogSkippingNoPipe(_logger);
            return;
        }

        Stream? stream = null;
        try
        {
            stream = await _transport.ConnectAsync(
                pipeName, connectTimeoutMs, PipeDirection.Out, cancellationToken).ConfigureAwait(false);

            if (handshake.Length > 0)
            {
                await stream.WriteAsync(handshake, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            if (_disposed)
            {
                await stream.DisposeAsync().ConfigureAwait(false);
                return;
            }

            _stream = stream;
            stream = null;
        }
        catch (OperationCanceledException)
        {
            if (stream is not null)
            {
                await stream.DisposeAsync().ConfigureAwait(false);
                stream = null;
            }
            throw;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            LogConnectFailed(_logger, pipeName, ex);
        }
        finally
        {
            if (stream is not null)
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
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

        var stream = _stream;
        _stream = null;
        if (stream is not null)
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Keep-alive pipe not configured; skipping connection.")]
    private static partial void LogSkippingNoPipe(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "Failed to connect keep-alive pipe '{PipeName}'.")]
    private static partial void LogConnectFailed(ILogger logger, string pipeName, Exception ex);
}
