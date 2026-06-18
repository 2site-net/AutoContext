namespace AutoContext.Engine.Core.Endpoints;

using AutoContext.Engine.Core.Features.Instructions;
using AutoContext.Engine.Core.Features.McpTools;
using AutoContext.Engine.Core.Infrastructure;
using AutoContext.Engine.Core.Infrastructure.Storage;
using AutoContext.Engine.Core.Lifecycle;
using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Context;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.Messages.Config;
using AutoContext.Engine.Protocol.Messages.Instructions;
using AutoContext.Framework.Pipes;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Hosted service that owns the engine's four named-pipe accept
/// loops — one each for <see cref="EndpointKind.Rpc"/>,
/// <see cref="EndpointKind.Events"/>,
/// <see cref="EndpointKind.Health"/>, and
/// <see cref="EndpointKind.Logs"/>. Per
/// <c>design § Lifecycle &gt; Pipe topology</c> the four pipes are
/// bound atomically at startup; if any bind fails the host fails
/// fast (the same launcher-bug shape that the injected
/// <see cref="IUniqueInstanceGuard"/> guards against in the
/// pre-bind probe).
/// </summary>
/// <remarks>
/// <para>
/// Each accepted connection is delegated whole to a per-kind
/// handler: <see cref="RpcEndpointHandler"/> for <c>rpc</c>,
/// <see cref="EventsEndpointHandler"/> for <c>events</c>, and
/// <see cref="LogsEndpointHandler"/> for <c>logs</c>. <c>health</c>
/// is a passive observer surface the service binds but does not
/// author payloads for. The service itself owns only listener
/// orchestration — the four-pipe bind, the accept loops, and the
/// graceful-stop sequence that publishes the terminal lifecycle
/// event and arms the shared <see cref="ShutdownDrainDeadline"/>.
/// </para>
/// <para>
/// The OS pipe name is the canonical <see cref="Endpoint"/> wire
/// form verbatim —
/// <c>autocontext-engine:&lt;kind&gt;@&lt;workspaceHash&gt;#&lt;instanceId&gt;</c>
/// — because Windows named-pipe names allow every character except
/// <c>\</c> (case-insensitive match), and POSIX socket basenames
/// accept the same shape unchanged. Keeping the wire form
/// byte-for-byte identical to the pipe name removes a class of
/// drift between the design's endpoint format and the bytes the
/// transport actually binds.
/// </para>
/// <para>
/// <see cref="StartAsync"/> invokes the injected
/// <see cref="IUniqueInstanceGuard"/> before the four-pipe bind:
/// reusing an <c>--instance-id</c> while another engine is alive
/// is a launcher bug, and the guard turns the common case (a peer
/// is already up at this engine's address) into a clear
/// diagnostic instead of an opaque bind error.
/// </para>
/// </remarks>
internal sealed partial class EndpointHostService : IHostedService, IAsyncDisposable
{
    private static readonly EndpointKind[] AllKinds =
    [
        EndpointKind.Rpc,
        EndpointKind.Events,
        EndpointKind.Health,
        EndpointKind.Logs,
    ];

    private int _disposed;
    private readonly ShutdownDrainDeadline _drainDeadline;
    private readonly EventsEndpointHandler _eventsEndpointHandler;
    private readonly IUniqueInstanceGuard _instanceGuard;
    private readonly LifecycleNotifier _lifecycleNotifier;
    private readonly Dictionary<EndpointKind, BoundPipeListener> _listeners = new(AllKinds.Length);
    private readonly ILogger<EndpointHostService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly LogsEndpointHandler _logsEndpointHandler;
    private readonly EngineOptions _options;
    private readonly RpcEndpointHandler _rpcEndpointHandler;
    private readonly List<Task> _runTasks = new(AllKinds.Length);
    private int _started;
    private int _stopped;
    private CancellationTokenSource? _stoppingCts;

    /// <summary>
    /// Creates a new <see cref="EndpointHostService"/>.
    /// </summary>
    /// <param name="options">Engine options resolved from the
    /// host's options pipeline.</param>
    /// <param name="loggerFactory">Logger factory used to create
    /// loggers for this service and the underlying pipe listeners.</param>
    /// <param name="lifecycleNotifier">Notifier that stamps engine
    /// identity onto lifecycle events; <see cref="StopAsync"/>
    /// invokes <see cref="LifecycleNotifier.NotifyShutdown"/> ahead
    /// of pipe teardown so subscribers see the terminal frame.</param>
    /// <param name="instanceGuard">Pre-bind probe asserting no
    /// other engine currently owns this engine's would-be
    /// endpoint address. Invoked at the top of
    /// <see cref="StartAsync"/> before the four-pipe bind so
    /// the launcher-bug case (fresh-UUID violation) surfaces
    /// as a clear diagnostic instead of an opaque bind error.</param>
    /// <param name="rpcEndpointHandler">Handler that drives each
    /// accepted <c>rpc</c> connection end-to-end — handshake, idle
    /// keep-alive, and the post-handshake JSON-RPC dispatch loop.</param>
    /// <param name="eventsEndpointHandler">Handler that drives each
    /// accepted <c>events</c> connection end-to-end — handshake, idle
    /// keep-alive, and the lifecycle-event subscription pump.</param>
    /// <param name="logsEndpointHandler">Handler that drives each
    /// accepted <c>logs</c> connection — the broadcaster subscription
    /// pump (no handshake, no keep-alive).</param>
    /// <param name="drainDeadline">Shared shutdown-drain deadline the
    /// host arms during <see cref="StopAsync"/> and the events/logs
    /// pumps observe so a peer that stops reading mid-shutdown cannot
    /// wedge teardown.</param>
    /// <exception cref="ArgumentNullException">
    /// Any constructor argument is <see langword="null"/>.
    /// </exception>
    public EndpointHostService(
        IOptions<EngineOptions> options,
        ILoggerFactory loggerFactory,
        LifecycleNotifier lifecycleNotifier,
        IUniqueInstanceGuard instanceGuard,
        RpcEndpointHandler rpcEndpointHandler,
        EventsEndpointHandler eventsEndpointHandler,
        LogsEndpointHandler logsEndpointHandler,
        ShutdownDrainDeadline drainDeadline)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(lifecycleNotifier);
        ArgumentNullException.ThrowIfNull(instanceGuard);
        ArgumentNullException.ThrowIfNull(rpcEndpointHandler);
        ArgumentNullException.ThrowIfNull(eventsEndpointHandler);
        ArgumentNullException.ThrowIfNull(logsEndpointHandler);
        ArgumentNullException.ThrowIfNull(drainDeadline);

        _options = options.Value;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<EndpointHostService>();
        _lifecycleNotifier = lifecycleNotifier;
        _instanceGuard = instanceGuard;
        _rpcEndpointHandler = rpcEndpointHandler;
        _eventsEndpointHandler = eventsEndpointHandler;
        _logsEndpointHandler = logsEndpointHandler;
        _drainDeadline = drainDeadline;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (_started != 0 && _stopped == 0)
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        else
        {
            _stoppingCts?.Dispose();
            _stoppingCts = null;
            _drainDeadline.Release();
        }
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            throw new InvalidOperationException(
                "EndpointHostService.StartAsync has already been invoked.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        await _instanceGuard.EnsureUniqueAsync(cancellationToken).ConfigureAwait(false);

        var workspaceHash = WorkspaceHash.Compute(_options.WorkspacePath);
        var instanceId = _options.InstanceId;

        _stoppingCts = new CancellationTokenSource();
        _drainDeadline.Reset();

        try
        {
            BindAll(workspaceHash, instanceId);
        }
        catch
        {
            // Bind failure on any of the four pipes is fatal — tear
            // down whichever listeners we'd already claimed and let
            // the host see the original exception.
            await DisposeListenersAsync().ConfigureAwait(false);
            _stoppingCts.Dispose();
            _stoppingCts = null;
            _drainDeadline.Release();
            throw;
        }

        var runToken = _stoppingCts.Token;

        foreach (var (kind, listener) in _listeners)
        {
            _runTasks.Add(RunAcceptLoopAsync(kind, listener, runToken));
        }

        LogStarted(_logger, workspaceHash, instanceId);
    }

    /// <summary>
    /// Stops the service: publishes the terminal
    /// <c>shutting-down</c> lifecycle event, cancels accept loops,
    /// and disposes the pipe listeners.
    /// </summary>
    /// <remarks>
    /// The terminal event is published <em>before</em> listener
    /// teardown so every connected <c>events</c>-pipe subscriber
    /// gets a chance to read it. The writer loops that flush the
    /// frame do not observe the accept-loop stop token (cancelling
    /// it would tear the connection down before the terminal frame
    /// reached the wire); instead they observe the shared
    /// <see cref="ShutdownDrainDeadline"/> that fires after
    /// <see cref="EngineOptions.ShutdownDrainTimeout"/>. A peer
    /// that fails to read the frame within that window has its
    /// pending write cancelled and the connection closed, so this
    /// method returns in bounded time regardless of peer
    /// behaviour. Peers that drain promptly observe the frame
    /// followed by EOF as usual.
    /// </remarks>
    /// <param name="cancellationToken">Token observed by accept
    /// loops and listener disposal. The events and logs pump loops
    /// observe the shared <see cref="ShutdownDrainDeadline"/>
    /// instead.</param>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
        {
            return;
        }

        // Publish shutting-down BEFORE cancelling the stop CTS so
        // the events-pipe writer loops (which intentionally do not
        // observe the stop token) drain the queued frame and flush
        // it to the wire while the listener still considers the
        // handler in-flight. The listener.RunAsync below waits for
        // every in-flight handler to finish before returning.
        _ = _lifecycleNotifier.NotifyShutdown();

        // Arm the drain deadline. Peers that read the terminal
        // frame before this fires complete the pump naturally;
        // peers that don't have their pending WriteAsync cancelled,
        // which lets listener teardown proceed.
        await _drainDeadline.StartDeadlineAsync(_options.ShutdownDrainTimeout).ConfigureAwait(false);

        if (_stoppingCts is { } cts)
        {
            await cts.CancelAsync().ConfigureAwait(false);
        }

        if (_runTasks.Count > 0)
        {
            try
            {
                await Task.WhenAll(_runTasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when the accept loops observe cancellation.
            }
        }

        foreach (var listener in _listeners.Values)
        {
            await listener.DisposeAsync().ConfigureAwait(false);
        }

        _listeners.Clear();
        _runTasks.Clear();
        _stoppingCts?.Dispose();
        _stoppingCts = null;
        _drainDeadline.Release();

        LogStopped(_logger);
    }

    /// <summary>
    /// Computes the OS pipe name the engine binds for
    /// <paramref name="kind"/> against <paramref name="workspaceHash"/>
    /// and <paramref name="instanceId"/>. The result is the canonical
    /// <see cref="Endpoint"/> wire form verbatim.
    /// </summary>
    private static string CreatePipeName(
        EndpointKind kind,
        WorkspaceHash workspaceHash,
        Guid instanceId)
    {
        if (workspaceHash.IsEmpty)
        {
            throw new ArgumentException(
                "WorkspaceHash must not be the default value.",
                nameof(workspaceHash));
        }

        return new Endpoint(kind, workspaceHash.Value, instanceId).ToString();
    }

    [LoggerMessage(EventId = 4, Level = LogLevel.Error,
        Message = "Accept loop for '{Kind}' endpoint faulted; the engine will shut down.")]
    private static partial void LogAcceptLoopFaulted(ILogger logger, Exception exception, EndpointKind kind);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug,
        Message = "Accepted connection on '{Kind}' endpoint.")]
    private static partial void LogConnectionAccepted(ILogger logger, EndpointKind kind);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "EndpointHostService bound four pipes for workspace '{WorkspaceHash}' instance {InstanceId:D}.")]
    private static partial void LogStarted(ILogger logger, WorkspaceHash workspaceHash, Guid instanceId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "EndpointHostService stopped; all pipe accept loops have drained.")]
    private static partial void LogStopped(ILogger logger);

    private void BindAll(WorkspaceHash workspaceHash, Guid instanceId)
    {
        var pipeLogger = _loggerFactory.CreateLogger<PipeListener>();

        foreach (var kind in AllKinds)
        {
            var pipeName = CreatePipeName(kind, workspaceHash, instanceId);
            var listener = new PipeListener(pipeName, pipeLogger);

            // Bind throws before the listener owns any OS resources,
            // so there is nothing extra to dispose for the failing
            // kind — the caller unwinds whatever we have so far.
            _listeners[kind] = listener.Bind();
        }
    }

    private async ValueTask DisposeListenersAsync()
    {
        foreach (var listener in _listeners.Values)
        {
            await listener.DisposeAsync().ConfigureAwait(false);
        }

        _listeners.Clear();
    }

    private async Task HandleConnectionAsync(
        EndpointKind kind,
        Stream stream,
        CancellationToken cancellationToken)
    {
        LogConnectionAccepted(_logger, kind);

        // health is a passive observer surface — accept-and-close
        // until later commits attach a payload.
        if (kind is EndpointKind.Health)
        {
            return;
        }

        // logs is also passive (no handshake) but pumps drained
        // records out of the broadcaster onto the wire as NDJSON
        // LogStreamFrame values until the broadcaster completes
        // (graceful shutdown), the pipe write faults (peer
        // disconnected), or the drain deadline fires (peer stopped
        // reading during shutdown).
        if (kind is EndpointKind.Logs)
        {
            await _logsEndpointHandler.HandleAsync(stream).ConfigureAwait(false);
            return;
        }

        // rpc owns its full connection lifecycle — handshake, idle
        // keep-alive, and the post-handshake JSON-RPC dispatch loop —
        // inside RpcEndpointHandler.
        if (kind is EndpointKind.Rpc)
        {
            await _rpcEndpointHandler.HandleAsync(stream, cancellationToken).ConfigureAwait(false);
            return;
        }

        // events owns its full connection lifecycle — handshake, idle
        // keep-alive, and the lifecycle-event subscription pump —
        // inside EventsEndpointHandler.
        await _eventsEndpointHandler.HandleAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    private async Task RunAcceptLoopAsync(
        EndpointKind kind,
        BoundPipeListener listener,
        CancellationToken cancellationToken)
    {
        try
        {
            await listener.RunAsync(
                (stream, ct) => HandleConnectionAsync(kind, stream, ct),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            LogAcceptLoopFaulted(_logger, ex, kind);
            throw;
        }
    }
}
