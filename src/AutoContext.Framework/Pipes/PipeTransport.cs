namespace AutoContext.Framework.Pipes;

using System.IO.Pipes;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Layer 1 transport primitive for AutoContext named-pipe communication
/// (Windows named pipes / Unix domain sockets via the BCL). Provides a
/// uniform <see cref="ConnectAsync"/> entry point so endpoint clients
/// (logging, health-monitor, worker control, configuration, request /
/// response) do not each have to construct and connect a
/// <see cref="NamedPipeClientStream"/> by hand.
/// </summary>
/// <remarks>
/// Phase 1 scope: client-side <see cref="ConnectAsync"/> only. The
/// server-side accept-loop primitive will be introduced when the first
/// server endpoint is migrated (Phase 4 in the unification plan).
/// </remarks>
public sealed partial class PipeTransport
{
    private readonly ILogger<PipeTransport> _logger;

    /// <summary>
    /// Creates a new <see cref="PipeTransport"/>. The
    /// <paramref name="logger"/> is mandatory; pass
    /// <see cref="NullLogger{PipeTransport}.Instance"/> for silent
    /// operation.
    /// </summary>
    public PipeTransport(ILogger<PipeTransport> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
    }

    /// <summary>
    /// Opens a connection to the named pipe identified by
    /// <paramref name="pipeName"/> on the local machine.
    /// </summary>
    /// <param name="pipeName">The pipe name (without the
    /// <c>\\.\pipe\</c> prefix on Windows). Must be non-empty.</param>
    /// <param name="timeoutMs">Connect timeout in milliseconds. Use
    /// <c>0</c> or a negative value to wait indefinitely (subject to
    /// <paramref name="cancellationToken"/>).</param>
    /// <param name="direction">Pipe direction. Defaults to
    /// <see cref="PipeDirection.InOut"/>.</param>
    /// <param name="cancellationToken">Cooperative cancellation. The
    /// returned task transitions to canceled if the token fires before
    /// the connection completes.</param>
    /// <returns>A connected <see cref="Stream"/> owned by the caller;
    /// dispose it to close the pipe.</returns>
    /// <exception cref="TimeoutException">The connect attempt did not
    /// complete within <paramref name="timeoutMs"/>.</exception>
    /// <exception cref="IOException">The remote side rejected the
    /// connection or the underlying transport failed.</exception>
    /// <exception cref="UnauthorizedAccessException">The current
    /// principal lacks permission to open the pipe.</exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was canceled before the
    /// connection completed.</exception>
    public async Task<Stream> ConnectAsync(
        string pipeName,
        int timeoutMs = 0,
        PipeDirection direction = PipeDirection.InOut,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);

        var pipe = new NamedPipeClientStream(
            serverName: ".",
            pipeName: pipeName,
            direction: direction,
            options: PipeOptions.Asynchronous);

        try
        {
            if (timeoutMs > 0)
            {
                await pipe.ConnectAsync(timeoutMs, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await pipe.ConnectAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        LogConnected(_logger, pipeName);
        return pipe;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "PipeTransport connected to '{PipeName}'.")]
    private static partial void LogConnected(ILogger logger, string pipeName);
}
