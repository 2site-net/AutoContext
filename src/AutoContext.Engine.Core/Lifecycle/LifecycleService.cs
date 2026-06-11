namespace AutoContext.Engine.Core.Lifecycle;

using System.Text.Json;

using AutoContext.Engine.Core.Features.Instructions;
using AutoContext.Engine.Core.Infrastructure;
using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Core.Infrastructure.Storage;
using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Core.Rpc;
using AutoContext.Engine.Core.Rpc.Policies;
using AutoContext.Engine.Core.Watchdogs;
using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Context;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages.Config;
using AutoContext.Engine.Protocol.Messages.Instructions;
using AutoContext.Engine.Protocol.Messages.Lifecycle;
using AutoContext.Engine.Protocol.Messages.Logs;
using AutoContext.Engine.Protocol.Serialization;
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
/// On every <c>rpc</c> and <c>events</c> connection, the service
/// performs the <c>Engine.Hello</c> handshake, then runs the
/// post-handshake JSON-RPC dispatch loop on <c>rpc</c> (handling
/// <c>Engine.RegistryEntries</c> and <c>Engine.Shutdown</c>) and
/// the subscription pump on <c>events</c> (enrolling the
/// connection with the <see cref="LifecycleEventStream"/> and
/// serialising each fanned-out <see cref="JsonLifecycleEvent"/>
/// into an <c>Engine.Lifecycle</c> JSON-RPC notification frame).
/// <c>health</c> and <c>logs</c> are passive observer surfaces
/// the service binds but does not author payloads for.
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
internal sealed partial class LifecycleService : IHostedService, IAsyncDisposable
{
    private static readonly EndpointKind[] AllKinds =
    [
        EndpointKind.Rpc,
        EndpointKind.Events,
        EndpointKind.Health,
        EndpointKind.Logs,
    ];

    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly IConfigSnapshotAccessor _configAccessor;
    private readonly SnapshotBroadcaster<JsonConfigSnapshot> _configBroadcaster;
    private readonly IConfigUpdater _configUpdater;
    private int _disposed;
    private CancellationTokenSource? _drainCts;
    private readonly LifecycleEventStream _eventStream;
    private readonly LifecycleFrameStream _eventFrameStream = new();
    private readonly IdleTimeoutWatchdog _idleTimeoutWatchdog;
    private readonly IUniqueInstanceGuard _instanceGuard;
    private readonly LifecycleNotifier _lifecycleNotifier;
    private readonly Dictionary<EndpointKind, BoundPipeListener> _listeners = new(AllKinds.Length);
    private readonly ILogger<LifecycleService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Broadcaster<JsonLogRecord> _logsBroadcaster;
    private readonly LogFrameStream _logFrameStream = new();
    private readonly EngineLogFileReader _logFileReader;
    private readonly EngineOptions _options;
    private readonly RegistryFileReader _registryReader;
    private readonly List<Task> _runTasks = new(AllKinds.Length);
    private int _started;
    private int _stopped;
    private CancellationTokenSource? _stoppingCts;
    private readonly IWorkspaceContextAccessor _workspaceAccessor;
    private readonly IInstructionsManifestAccessor _manifestAccessor;
    private readonly IInstructionsOverridesAccessor _overridesAccessor;
    private readonly InstructionsBodyProjector _bodyProjector;
    private readonly InstructionsFileReader _fileReader;
    private readonly InstructionsFullTextSearchService _searchService;
    private readonly SnapshotBroadcaster<IReadOnlyList<JsonInstructionsListRow>> _instructionsBroadcaster;

    /// <summary>
    /// Creates a new <see cref="LifecycleService"/>.
    /// </summary>
    /// <param name="options">Engine options resolved from the
    /// host's options pipeline.</param>
    /// <param name="loggerFactory">Logger factory used to create
    /// loggers for this service and the underlying pipe listeners.</param>
    /// <param name="applicationLifetime">Host lifetime the RPC
    /// dispatcher signals on a successful <c>Engine.Shutdown</c>
    /// request.</param>
    /// <param name="registryReader">Reader the RPC dispatcher uses
    /// to snapshot the machine-wide engine-liveness registry for
    /// <c>Engine.RegistryEntries</c>.</param>
    /// <param name="eventStream">Fan-out stream backing
    /// <c>Engine.Lifecycle.Subscribe</c>; every <c>events</c>-pipe
    /// connection enrolls a subscriber here.</param>
    /// <param name="lifecycleNotifier">Notifier that stamps engine
    /// identity onto lifecycle events; <see cref="StopAsync"/>
    /// invokes <see cref="LifecycleNotifier.NotifyShutdown"/> ahead
    /// of pipe teardown so subscribers see the terminal frame.</param>
    /// <param name="idleTimeoutWatchdog">Watchdog the service
    /// acquires a keep-alive token from for every accepted
    /// <c>rpc</c> and <c>events</c> connection (the only two
    /// endpoint kinds that pin the engine alive against the
    /// idle-timeout gate per
    /// <c>design § Lifecycle &gt; Idle shutdown</c>).</param>
    /// <param name="instanceGuard">Pre-bind probe asserting no
    /// other engine currently owns this engine's would-be
    /// endpoint address. Invoked at the top of
    /// <see cref="StartAsync"/> before the four-pipe bind so
    /// the launcher-bug case (fresh-UUID violation) surfaces
    /// as a clear diagnostic instead of an opaque bind error.</param>
    /// <param name="logsBroadcaster">Fan-out broadcaster backing the
    /// <c>logs</c> pipe; every accepted <c>logs</c>-pipe connection
    /// enrolls a subscriber here and pumps drained
    /// <see cref="JsonLogStreamFrame"/> values to the wire.</param>
    /// <param name="logFileReader">Forward-pass reader over the
    /// active <c>engine.log</c>; threaded into the RPC dispatch
    /// policy so the <c>Logs.GetEngine</c> handler can answer
    /// snapshot requests against the on-disk file.</param>
    /// <param name="configAccessor">Read-only view over the engine's
    /// in-memory config snapshot; threaded into the RPC dispatch
    /// policy so the <c>Config.Get</c> handler can answer with the
    /// current snapshot.</param>
    /// <param name="configUpdater">Write seam over the engine's
    /// in-memory config snapshot; threaded into the RPC dispatch
    /// policy so the <c>Config.ToggleFile</c> and
    /// <c>Config.ToggleRule</c> handlers can publish edits.</param>
    /// <param name="configBroadcaster">Fan-out broadcaster backing
    /// the <c>Config.Subscribe</c> RPC stream; threaded into the RPC
    /// dispatch policy so each subscribing connection enrolls a
    /// snapshot-seeded subscriber.</param>
    /// <param name="workspaceAccessor">Read-only view over the engine's
    /// in-memory workspace detection result; threaded into the RPC
    /// dispatch policy so the <c>Workspace.Detect</c> handler can answer
    /// with the current detection result.</param>
    /// <param name="manifestAccessor">Read-only view over the immutable
    /// instructions manifest snapshot; threaded into the RPC dispatch
    /// policy so the <c>Instructions.*</c> handlers can answer corpus
    /// listing and read requests.</param>
    /// <param name="overridesAccessor">Read-only view over the workspace
    /// instructions override inventory; threaded into the RPC dispatch
    /// policy so the <c>Instructions.List</c> handler can mark overridden
    /// files.</param>
    /// <param name="bodyProjector">Projects instruction bodies for the
    /// <c>Instructions.Get</c>, <c>GetAll</c>, and <c>GetAlwaysAttached</c>
    /// handlers.</param>
    /// <param name="fileReader">Reads verbatim on-disk instruction bodies
    /// for the <c>Instructions.GetRaw</c> handler.</param>
    /// <param name="searchService">Full-text search over the projected
    /// corpus backing the <c>Instructions.SearchContent</c>
    /// handler.</param>
    /// <param name="instructionsBroadcaster">Fan-out broadcaster backing
    /// the <c>Instructions.Subscribe</c> snapshot-on-subscribe stream.</param>
    /// <exception cref="ArgumentNullException">
    /// Any constructor argument is <see langword="null"/>.
    /// </exception>
    public LifecycleService(
        IOptions<EngineOptions> options,
        ILoggerFactory loggerFactory,
        IHostApplicationLifetime applicationLifetime,
        RegistryFileReader registryReader,
        LifecycleEventStream eventStream,
        LifecycleNotifier lifecycleNotifier,
        IdleTimeoutWatchdog idleTimeoutWatchdog,
        IUniqueInstanceGuard instanceGuard,
        Broadcaster<JsonLogRecord> logsBroadcaster,
        EngineLogFileReader logFileReader,
        IConfigSnapshotAccessor configAccessor,
        IConfigUpdater configUpdater,
        SnapshotBroadcaster<JsonConfigSnapshot> configBroadcaster,
        IWorkspaceContextAccessor workspaceAccessor,
        IInstructionsManifestAccessor manifestAccessor,
        IInstructionsOverridesAccessor overridesAccessor,
        InstructionsBodyProjector bodyProjector,
        InstructionsFileReader fileReader,
        InstructionsFullTextSearchService searchService,
        SnapshotBroadcaster<IReadOnlyList<JsonInstructionsListRow>> instructionsBroadcaster)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(applicationLifetime);
        ArgumentNullException.ThrowIfNull(registryReader);
        ArgumentNullException.ThrowIfNull(eventStream);
        ArgumentNullException.ThrowIfNull(lifecycleNotifier);
        ArgumentNullException.ThrowIfNull(idleTimeoutWatchdog);
        ArgumentNullException.ThrowIfNull(instanceGuard);
        ArgumentNullException.ThrowIfNull(logsBroadcaster);
        ArgumentNullException.ThrowIfNull(logFileReader);
        ArgumentNullException.ThrowIfNull(configAccessor);
        ArgumentNullException.ThrowIfNull(configUpdater);
        ArgumentNullException.ThrowIfNull(configBroadcaster);
        ArgumentNullException.ThrowIfNull(workspaceAccessor);
        ArgumentNullException.ThrowIfNull(manifestAccessor);
        ArgumentNullException.ThrowIfNull(overridesAccessor);
        ArgumentNullException.ThrowIfNull(bodyProjector);
        ArgumentNullException.ThrowIfNull(fileReader);
        ArgumentNullException.ThrowIfNull(searchService);
        ArgumentNullException.ThrowIfNull(instructionsBroadcaster);

        _options = options.Value;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<LifecycleService>();
        _applicationLifetime = applicationLifetime;
        _registryReader = registryReader;
        _eventStream = eventStream;
        _lifecycleNotifier = lifecycleNotifier;
        _idleTimeoutWatchdog = idleTimeoutWatchdog;
        _instanceGuard = instanceGuard;
        _logsBroadcaster = logsBroadcaster;
        _logFileReader = logFileReader;
        _configAccessor = configAccessor;
        _configUpdater = configUpdater;
        _configBroadcaster = configBroadcaster;
        _workspaceAccessor = workspaceAccessor;
        _manifestAccessor = manifestAccessor;
        _overridesAccessor = overridesAccessor;
        _bodyProjector = bodyProjector;
        _fileReader = fileReader;
        _searchService = searchService;
        _instructionsBroadcaster = instructionsBroadcaster;
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
            _drainCts?.Dispose();
            _drainCts = null;
        }
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            throw new InvalidOperationException(
                "LifecycleService.StartAsync has already been invoked.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        await _instanceGuard.EnsureUniqueAsync(cancellationToken).ConfigureAwait(false);

        var workspaceHash = WorkspaceHash.Compute(_options.WorkspacePath);
        var instanceId = _options.InstanceId;

        _stoppingCts = new CancellationTokenSource();
        _drainCts = new CancellationTokenSource();

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
            _drainCts.Dispose();
            _drainCts = null;
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
    /// reached the wire); instead they observe an internal
    /// drain-deadline token that fires after
    /// <see cref="EngineOptions.ShutdownDrainTimeout"/>. A peer
    /// that fails to read the frame within that window has its
    /// pending write cancelled and the connection closed, so this
    /// method returns in bounded time regardless of peer
    /// behaviour. Peers that drain promptly observe the frame
    /// followed by EOF as usual.
    /// </remarks>
    /// <param name="cancellationToken">Token observed by accept
    /// loops and listener disposal. The events-pipe writer loops
    /// observe an internal drain-deadline token instead.</param>
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
        if (_drainCts is { } drainCts)
        {
            var drainTimeout = _options.ShutdownDrainTimeout;
            if (drainTimeout <= TimeSpan.Zero)
            {
                await drainCts.CancelAsync().ConfigureAwait(false);
            }
            else
            {
                drainCts.CancelAfter(drainTimeout);
            }
        }

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
        _drainCts?.Dispose();
        _drainCts = null;

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

    [LoggerMessage(EventId = 5, Level = LogLevel.Debug,
        Message = "Events-pipe write faulted; closing subscriber connection.")]
    private static partial void LogEventsPipeWriteFaulted(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 6, Level = LogLevel.Debug,
        Message = "Logs-pipe write faulted; closing subscriber connection.")]
    private static partial void LogLogsPipeWriteFaulted(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "LifecycleService bound four pipes for workspace '{WorkspaceHash}' instance {InstanceId:D}.")]
    private static partial void LogStarted(ILogger logger, WorkspaceHash workspaceHash, Guid instanceId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "LifecycleService stopped; all pipe accept loops have drained.")]
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
            await PumpLogsConnectionAsync(stream).ConfigureAwait(false);
            return;
        }

        var accepted = await RpcConnectionProcessor
            .RunAsync(stream, new HandshakePolicy(kind, _logger), _logger, cancellationToken)
            .ConfigureAwait(false);

        if (!accepted)
        {
            // Handshake refused; the listener disposes the stream
            // when this method returns, closing the connection.
            return;
        }

        // Keep-alive accounting per design § Lifecycle > Idle
        // shutdown: only post-handshake rpc and events connections
        // pin the engine alive against the idle-timeout gate.
        // health and logs short-circuit above and never reach
        // this point.
        var keepAlive = await _idleTimeoutWatchdog
            .AcquireKeepAliveAsync()
            .ConfigureAwait(false);
        await using (keepAlive.ConfigureAwait(false))
        {
            if (kind == EndpointKind.Rpc)
            {
                // Post-handshake RPC dispatch loop (row #9). Reads one
                // JSON-RPC frame at a time and routes it to the
                // matching handler until the peer closes the pipe,
                // cancellation is observed, or Engine.Shutdown is
                // honoured.
                _ = await RpcConnectionProcessor
                    .RunAsync(
                        stream,
                        new DispatchPolicy(_applicationLifetime, _registryReader, _logFileReader, _logsBroadcaster, _configAccessor, _configUpdater, _configBroadcaster, _workspaceAccessor, _manifestAccessor, _overridesAccessor, _bodyProjector, _fileReader, _searchService, _instructionsBroadcaster, _logger),
                        _logger,
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            }

            // Events: post-handshake subscription loop. Enrol with the
            // stream (which seeds the started event into our bounded
            // buffer), then pump every event the stream hands us onto
            // the wire as an Engine.Lifecycle notification until the
            // channel completes (graceful shutdown or unsubscribe), the
            // pipe write faults (client disconnected), or the drain
            // deadline fires (peer stopped reading during shutdown).
            //
            // The pump intentionally does NOT observe cancellationToken
            // (the accept-loop stop token). StopAsync drives shutdown
            // by publishing shutting-down via the notifier and arming
            // the drain deadline BEFORE cancelling the stop CTS, so the
            // pump exits cleanly after flushing the terminal frame or
            // after the deadline elapses — whichever comes first.
            await PumpEventsConnectionAsync(stream).ConfigureAwait(false);
        }
    }

    private async Task PumpEventsConnectionAsync(Stream stream)
    {
        var drainToken = _drainCts?.Token ?? CancellationToken.None;
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

    private async Task PumpLogsConnectionAsync(Stream stream)
    {
        var drainToken = _drainCts?.Token ?? CancellationToken.None;
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
