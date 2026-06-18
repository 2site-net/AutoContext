namespace AutoContext.Engine.Core.Endpoints;

using System.Text.Json;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Core.Logging;

using AutoContext.Engine.Protocol.Messages.Logs;
using AutoContext.Engine.Protocol.Serialization;

using AutoContext.Framework.Pipes;

using Microsoft.Extensions.Logging;

/// <summary>
/// Handles a single accepted <see cref="EndpointKind.Logs"/>
/// connection. The <c>logs</c> pipe is a passive observer surface:
/// there is no <c>Engine.Hello</c> handshake and no idle keep-alive
/// (a logs subscriber never pins the engine alive). The handler
/// enrols a broadcaster subscriber and pumps each drained
/// <see cref="JsonLogStreamFrame"/> onto the wire until the
/// broadcaster completes (graceful shutdown), the pipe write faults
/// (peer disconnected), or the shutdown drain deadline fires (peer
/// stopped reading during shutdown).
/// </summary>
internal sealed partial class LogsEndpointHandler
{
    private readonly ShutdownDrainDeadline _drainDeadline;
    private readonly LogFrameStream _logFrameStream = new();
    private readonly ILogger<LogsEndpointHandler> _logger;
    private readonly Broadcaster<JsonLogRecord> _logsBroadcaster;

    /// <summary>
    /// Creates a new <see cref="LogsEndpointHandler"/>.
    /// </summary>
    /// <param name="logsBroadcaster">Fan-out broadcaster backing the
    /// <c>logs</c> pipe; every accepted connection enrols a
    /// subscriber here.</param>
    /// <param name="drainDeadline">Shared shutdown-drain deadline whose
    /// token the pump observes so a peer that stops reading during
    /// shutdown cannot wedge teardown.</param>
    /// <param name="logger">Logger for pipe-write fault diagnostics.</param>
    /// <exception cref="ArgumentNullException">
    /// Any constructor argument is <see langword="null"/>.
    /// </exception>
    public LogsEndpointHandler(
        Broadcaster<JsonLogRecord> logsBroadcaster,
        ShutdownDrainDeadline drainDeadline,
        ILogger<LogsEndpointHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(logsBroadcaster);
        ArgumentNullException.ThrowIfNull(drainDeadline);
        ArgumentNullException.ThrowIfNull(logger);

        _logsBroadcaster = logsBroadcaster;
        _drainDeadline = drainDeadline;
        _logger = logger;
    }

    /// <summary>
    /// Drives one accepted <c>logs</c> connection: enrols a
    /// broadcaster subscriber and pumps drained log frames onto the
    /// wire until the stream completes, the write faults, or the
    /// drain deadline fires.
    /// </summary>
    /// <param name="stream">Connected pipe stream. The caller owns
    /// the stream lifetime; this method neither closes nor disposes
    /// it.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="stream"/> is <see langword="null"/>.
    /// </exception>
    public async Task HandleAsync(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var drainToken = _drainDeadline.Token;
        using var subscription = _logsBroadcaster.Subscribe();
        var codec = new LengthPrefixedFrameCodec(stream);

        try
        {
            await foreach (var frame in _logFrameStream
                .StreamAsync(subscription, drainToken)
                .ConfigureAwait(false))
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(
                    frame, ProtocolJsonContext.Default.JsonLogStreamFrame);

                await codec.WriteAsync(bytes, drainToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (drainToken.IsCancellationRequested)
        {
            // Drain deadline elapsed before the peer drained the
            // pending frames. The listener tears the connection
            // down when this method returns; nothing to report.
        }
        catch (IOException ex)
        {
            LogLogsPipeWriteFaulted(_logger, ex);
        }
        catch (ObjectDisposedException ex)
        {
            LogLogsPipeWriteFaulted(_logger, ex);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Logs-pipe write faulted; closing subscriber connection.")]
    private static partial void LogLogsPipeWriteFaulted(ILogger logger, Exception exception);
}
