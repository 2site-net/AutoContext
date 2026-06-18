namespace AutoContext.Engine.Core.Endpoints;

using AutoContext.Engine.Core.Rpc;
using AutoContext.Engine.Core.Rpc.Policies;
using AutoContext.Engine.Core.Watchdogs;
using AutoContext.Engine.Protocol;

using Microsoft.Extensions.Logging;

/// <summary>
/// Handles a single accepted <see cref="EndpointKind.Rpc"/>
/// connection end-to-end: the mandatory <c>Engine.Hello</c>
/// handshake, idle keep-alive accounting, and the post-handshake
/// JSON-RPC dispatch loop.
/// </summary>
/// <remarks>
/// <para>
/// The handshake and the dispatch loop share one shell —
/// <see cref="RpcConnectionProcessor"/> — and differ only in the
/// policy each supplies. A refused handshake closes the connection
/// without entering dispatch. An accepted handshake acquires a
/// keep-alive token from the <see cref="IdleTimeoutWatchdog"/> so
/// the connection pins the engine alive against the idle-timeout
/// gate (per <c>design § Lifecycle &gt; Idle shutdown</c>) for as
/// long as the dispatch loop runs, then releases it on exit.
/// </para>
/// <para>
/// A fresh <see cref="DispatchPolicy"/> is built per connection via
/// the injected <see cref="DispatchPolicyFactory"/> because the
/// policy owns per-connection frame-stream state.
/// </para>
/// </remarks>
internal sealed class RpcEndpointHandler : IEndpointHandler
{
    private readonly DispatchPolicyFactory _dispatchPolicyFactory;
    private readonly IdleTimeoutWatchdog _idleTimeoutWatchdog;
    private readonly ILogger<RpcEndpointHandler> _logger;

    /// <summary>
    /// Creates a new <see cref="RpcEndpointHandler"/>.
    /// </summary>
    /// <param name="dispatchPolicyFactory">Factory that builds a
    /// fresh <see cref="DispatchPolicy"/> for each accepted
    /// connection.</param>
    /// <param name="idleTimeoutWatchdog">Watchdog the handler
    /// acquires a keep-alive token from for the lifetime of the
    /// post-handshake dispatch loop.</param>
    /// <param name="logger">Logger for the handshake policy and the
    /// dispatch processor's internal diagnostics.</param>
    /// <exception cref="ArgumentNullException">
    /// Any constructor argument is <see langword="null"/>.
    /// </exception>
    public RpcEndpointHandler(
        DispatchPolicyFactory dispatchPolicyFactory,
        IdleTimeoutWatchdog idleTimeoutWatchdog,
        ILogger<RpcEndpointHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(dispatchPolicyFactory);
        ArgumentNullException.ThrowIfNull(idleTimeoutWatchdog);
        ArgumentNullException.ThrowIfNull(logger);

        _dispatchPolicyFactory = dispatchPolicyFactory;
        _idleTimeoutWatchdog = idleTimeoutWatchdog;
        _logger = logger;
    }

    /// <inheritdoc/>
    public EndpointKind Kind
        => EndpointKind.Rpc;

    /// <summary>
    /// Drives one accepted <c>rpc</c> connection: the
    /// <c>Engine.Hello</c> handshake, then — on acceptance — the
    /// keep-alive-guarded JSON-RPC dispatch loop.
    /// </summary>
    /// <param name="stream">Connected pipe stream. The caller owns
    /// the stream lifetime; this method neither closes nor disposes
    /// it.</param>
    /// <param name="cancellationToken">Token that aborts the
    /// handshake and dispatch loops on shutdown.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="stream"/> is <see langword="null"/>.
    /// </exception>
    public async Task HandleAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var accepted = await RpcConnectionProcessor
            .RunAsync(stream, new HandshakePolicy(EndpointKind.Rpc, _logger), _logger, cancellationToken)
            .ConfigureAwait(false);

        if (!accepted)
        {
            // Handshake refused; the caller's accept loop disposes
            // the stream when this method returns, closing the
            // connection.
            return;
        }

        // Keep-alive accounting per design § Lifecycle > Idle
        // shutdown: a post-handshake rpc connection pins the engine
        // alive against the idle-timeout gate for as long as it runs.
        var keepAlive = await _idleTimeoutWatchdog
            .AcquireKeepAliveAsync()
            .ConfigureAwait(false);

        await using (keepAlive.ConfigureAwait(false))
        {
            // Post-handshake RPC dispatch loop. Reads one JSON-RPC
            // frame at a time and routes it to the matching handler
            // until the peer closes the pipe, cancellation is
            // observed, or Engine.Shutdown is honoured.
            _ = await RpcConnectionProcessor
                .RunAsync(
                    stream,
                    _dispatchPolicyFactory.Create(),
                    _logger,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
