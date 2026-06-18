namespace AutoContext.Engine.Core.Endpoints;

using System.Text.Json;

using AutoContext.Engine.Core.Lifecycle;
using AutoContext.Engine.Core.Rpc;
using AutoContext.Engine.Core.Rpc.Policies;
using AutoContext.Engine.Core.Watchdogs;

using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages.Lifecycle;
using AutoContext.Engine.Protocol.Serialization;

using AutoContext.Framework.Pipes;

using Microsoft.Extensions.Logging;

/// <summary>
/// Handles a single accepted <see cref="EndpointKind.Events"/>
/// connection end-to-end: the mandatory <c>Engine.Hello</c>
/// handshake, idle keep-alive accounting, and the post-handshake
/// subscription pump that fans <see cref="JsonLifecycleEvent"/>
/// values out as <c>Engine.Lifecycle</c> JSON-RPC notifications.
/// </summary>
/// <remarks>
/// <para>
/// An accepted handshake acquires a keep-alive token from the
/// <see cref="IdleTimeoutWatchdog"/> so the connection pins the
/// engine alive against the idle-timeout gate (per
/// <c>design § Lifecycle &gt; Idle shutdown</c>) for as long as the
/// pump runs, then releases it on exit. A refused handshake closes
/// the connection without entering the pump.
/// </para>
/// <para>
/// The pump deliberately does <em>not</em> observe the accept-loop
/// stop token. Shutdown is driven by
/// <see cref="EndpointHostService.StopAsync"/>, which publishes the
/// terminal <c>shutting-down</c> event and arms the shared
/// <see cref="ShutdownDrainDeadline"/> <em>before</em> cancelling the
/// accept-loop stop token. The pump therefore observes the drain
/// token, exiting cleanly after flushing the terminal frame or after
/// the drain deadline elapses — whichever comes first.
/// </para>
/// </remarks>
internal sealed partial class EventsEndpointHandler
{
    private readonly ShutdownDrainDeadline _drainDeadline;
    private readonly LifecycleFrameStream _eventFrameStream = new();
    private readonly LifecycleEventStream _eventStream;
    private readonly IdleTimeoutWatchdog _idleTimeoutWatchdog;
    private readonly ILogger<EventsEndpointHandler> _logger;

    /// <summary>
    /// Creates a new <see cref="EventsEndpointHandler"/>.
    /// </summary>
    /// <param name="eventStream">Fan-out stream backing
    /// <c>Engine.Lifecycle.Subscribe</c>; every accepted connection
    /// enrols a subscriber here, which seeds the current
    /// <c>started</c> event into the connection's bounded buffer.</param>
    /// <param name="idleTimeoutWatchdog">Watchdog the handler
    /// acquires a keep-alive token from for the lifetime of the
    /// post-handshake subscription pump.</param>
    /// <param name="drainDeadline">Shared shutdown-drain deadline whose
    /// token the pump observes so a peer that stops reading during
    /// shutdown cannot wedge teardown.</param>
    /// <param name="logger">Logger for the handshake policy and
    /// pipe-write fault diagnostics.</param>
    /// <exception cref="ArgumentNullException">
    /// Any constructor argument is <see langword="null"/>.
    /// </exception>
    public EventsEndpointHandler(
        LifecycleEventStream eventStream,
        IdleTimeoutWatchdog idleTimeoutWatchdog,
        ShutdownDrainDeadline drainDeadline,
        ILogger<EventsEndpointHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(eventStream);
        ArgumentNullException.ThrowIfNull(idleTimeoutWatchdog);
        ArgumentNullException.ThrowIfNull(drainDeadline);
        ArgumentNullException.ThrowIfNull(logger);

        _eventStream = eventStream;
        _idleTimeoutWatchdog = idleTimeoutWatchdog;
        _drainDeadline = drainDeadline;
        _logger = logger;
    }

    /// <summary>
    /// Drives one accepted <c>events</c> connection: the
    /// <c>Engine.Hello</c> handshake, then — on acceptance — the
    /// keep-alive-guarded subscription pump.
    /// </summary>
    /// <param name="stream">Connected pipe stream. The caller owns
    /// the stream lifetime; this method neither closes nor disposes
    /// it.</param>
    /// <param name="cancellationToken">Token that aborts the
    /// handshake on shutdown. The post-handshake pump observes the
    /// shared drain deadline instead.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="stream"/> is <see langword="null"/>.
    /// </exception>
    public async Task HandleAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var accepted = await RpcConnectionProcessor
            .RunAsync(stream, new HandshakePolicy(EndpointKind.Events, _logger), _logger, cancellationToken)
            .ConfigureAwait(false);

        if (!accepted)
        {
            // Handshake refused; the caller's accept loop disposes
            // the stream when this method returns, closing the
            // connection.
            return;
        }

        // Keep-alive accounting per design § Lifecycle > Idle
        // shutdown: a post-handshake events connection pins the
        // engine alive against the idle-timeout gate for as long as
        // it runs.
        var keepAlive = await _idleTimeoutWatchdog
            .AcquireKeepAliveAsync()
            .ConfigureAwait(false);

        await using (keepAlive.ConfigureAwait(false))
        {
            await PumpAsync(stream).ConfigureAwait(false);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Events-pipe write faulted; closing subscriber connection.")]
    private static partial void LogEventsPipeWriteFaulted(ILogger logger, Exception exception);

    private async Task PumpAsync(Stream stream)
    {
        var drainToken = _drainDeadline.Token;
        using var subscription = _eventStream.Subscribe();
        var codec = new LengthPrefixedFrameCodec(stream);

        try
        {
            await foreach (var evt in _eventFrameStream
                .StreamAsync(subscription, drainToken)
                .ConfigureAwait(false))
            {
                var paramsElement = JsonSerializer.SerializeToElement(
                    evt, ProtocolJsonContext.Default.JsonLifecycleEvent);
                var notification = new JsonRpcNotification
                {
                    Method = LifecycleMethods.Notification,
                    Params = paramsElement,
                };
                var bytes = JsonSerializer.SerializeToUtf8Bytes(
                    notification, ProtocolJsonContext.Default.JsonRpcNotification);

                await codec.WriteAsync(bytes, drainToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (drainToken.IsCancellationRequested)
        {
            // Drain deadline elapsed before the peer read the
            // terminal frame. The connection will be torn down
            // when the listener disposes; nothing to report.
        }
        catch (IOException ex)
        {
            LogEventsPipeWriteFaulted(_logger, ex);
        }
        catch (ObjectDisposedException ex)
        {
            LogEventsPipeWriteFaulted(_logger, ex);
        }
    }
}
