namespace AutoContext.Engine.Core.Lifecycle;

using System.Text.Json;

using AutoContext.Engine.Core.Infrastructure.Primitives;
using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages.Lifecycle;
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
/// fast (the same launcher-bug shape that the
/// <c>InstanceIdCollisionWatchdog</c> guards against later in the
/// phase).
/// </summary>
/// <remarks>
/// <para>
/// Per the Phase 1 commit sequence, the per-pipe handlers are added
/// incrementally: this commit performs the <c>Engine.Hello</c>
/// handshake on every <c>rpc</c> and <c>events</c> connection,
/// then runs the post-handshake JSON-RPC dispatch loop on
/// <c>rpc</c> (handling <c>Engine.RegistryEntries</c> and
/// <c>Engine.Shutdown</c>) and the subscription pump on
/// <c>events</c> (enrolling the connection with the
/// <see cref="LifecycleEventStream"/> and serialising each
/// fanned-out <see cref="LifecycleEvent"/>
/// into an <c>Engine.Lifecycle</c> JSON-RPC notification frame).
/// <c>health</c> and <c>logs</c> remain accept-and-close at this
/// stage — they are passive observer surfaces whose payloads land
/// in later commits.
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
    private int _disposed;
    private readonly LifecycleEventStream _eventStream;
    private readonly LifecycleNotifier _lifecycleNotifier;
    private readonly Dictionary<EndpointKind, BoundPipeListener> _listeners = new(AllKinds.Length);
    private readonly ILogger<LifecycleService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly EngineOptions _options;
    private readonly RegistryFileReader _registryReader;
    private readonly List<Task> _runTasks = new(AllKinds.Length);
    private int _started;
    private int _stopped;
    private CancellationTokenSource? _stoppingCts;

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
    /// <exception cref="ArgumentNullException">
    /// Any constructor argument is <see langword="null"/>.
    /// </exception>
    public LifecycleService(
        IOptions<EngineOptions> options,
        ILoggerFactory loggerFactory,
        IHostApplicationLifetime applicationLifetime,
        RegistryFileReader registryReader,
        LifecycleEventStream eventStream,
        LifecycleNotifier lifecycleNotifier)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(applicationLifetime);
        ArgumentNullException.ThrowIfNull(registryReader);
        ArgumentNullException.ThrowIfNull(eventStream);
        ArgumentNullException.ThrowIfNull(lifecycleNotifier);

        _options = options.Value;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<LifecycleService>();
        _applicationLifetime = applicationLifetime;
        _registryReader = registryReader;
        _eventStream = eventStream;
        _lifecycleNotifier = lifecycleNotifier;
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

        var workspaceHash = WorkspaceHash.Compute(_options.WorkspacePath);
        var instanceId = _options.InstanceId;

        _stoppingCts = new CancellationTokenSource();

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
            throw;
        }

        var runToken = _stoppingCts.Token;

        foreach (var (kind, listener) in _listeners)
        {
            _runTasks.Add(RunAcceptLoopAsync(kind, listener, runToken));
        }

        LogStarted(_logger, workspaceHash, instanceId);
    }

    /// <inheritdoc/>
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

        // health and logs are passive observer surfaces — no
        // handshake; payload emission arrives in later commits.
        if (kind is EndpointKind.Health or EndpointKind.Logs)
        {
            return;
        }

        var accepted = await ConnectionHandshake
            .TryAcceptAsync(stream, kind, _logger, cancellationToken)
            .ConfigureAwait(false);

        if (!accepted)
        {
            // Handshake refused; the listener disposes the stream
            // when this method returns, closing the connection.
            return;
        }

        if (kind == EndpointKind.Rpc)
        {
            // Post-handshake RPC dispatch loop (row #9). Reads one
            // JSON-RPC frame at a time and routes it to the
            // matching handler until the peer closes the pipe,
            // cancellation is observed, or Engine.Shutdown is
            // honoured.
            await RpcDispatcher
                .DispatchAsync(
                    stream,
                    _applicationLifetime,
                    _registryReader,
                    _logger,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        // Events: post-handshake subscription loop. Enrol with the
        // stream (which seeds the started event into our bounded
        // buffer), then pump every event the stream hands us onto
        // the wire as an Engine.Lifecycle notification until the
        // channel completes (graceful shutdown or unsubscribe) or
        // the pipe write faults (client disconnected).
        //
        // The pump intentionally does NOT observe cancellationToken
        // on the read side: StopAsync drives shutdown by publishing
        // shutting-down via the notifier (which completes the
        // channel) BEFORE cancelling the stop CTS, so the pump
        // exits cleanly after flushing the terminal frame.
        await PumpEventsConnectionAsync(stream).ConfigureAwait(false);
    }

    private async Task PumpEventsConnectionAsync(Stream stream)
    {
        using var subscription = _eventStream.Subscribe();
        var codec = new LengthPrefixedFrameCodec(stream);

        try
        {
            await foreach (var evt in subscription
                .ReadAllAsync(CancellationToken.None)
                .ConfigureAwait(false))
            {
                var paramsElement = JsonSerializer.SerializeToElement(
                    evt, ProtocolJsonContext.Default.LifecycleEvent);
                var notification = new JsonRpcNotification
                {
                    Method = LifecycleMethods.Notification,
                    Params = paramsElement,
                };
                var bytes = JsonSerializer.SerializeToUtf8Bytes(
                    notification, ProtocolJsonContext.Default.JsonRpcNotification);

                await codec.WriteAsync(bytes, CancellationToken.None).ConfigureAwait(false);
            }
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
