namespace AutoContext.Engine.Core.Lifecycle;

using AutoContext.Engine.Core.Infrastructure.Primitives;
using AutoContext.Framework.Pipes;
using AutoContext.Engine.Protocol;

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
/// handshake on every <c>rpc</c> and <c>events</c> connection and
/// then holds the connection open until cancellation (the
/// post-handshake RPC dispatcher arrives in row #9 and the events
/// subscription loop in row #10). <c>health</c> and <c>logs</c>
/// remain accept-and-close at this stage — they are passive
/// observer surfaces whose payloads land in later commits.
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

    private readonly EngineOptions _options;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<LifecycleService> _logger;
    private readonly Dictionary<EndpointKind, BoundPipeListener> _listeners = new(AllKinds.Length);
    private readonly List<Task> _runTasks = new(AllKinds.Length);

    private CancellationTokenSource? _stoppingCts;
    private int _started;
    private int _stopped;
    private int _disposed;

    /// <summary>
    /// Creates a new <see cref="LifecycleService"/>.
    /// </summary>
    /// <param name="options">Engine options resolved from the
    /// host's options pipeline.</param>
    /// <param name="loggerFactory">Logger factory used to build the
    /// service's own logger and the per-listener loggers.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/> or <paramref name="loggerFactory"/>
    /// is <see langword="null"/>.
    /// </exception>
    public LifecycleService(
        IOptions<EngineOptions> options,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _options = options.Value;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<LifecycleService>();
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

        // Hold the connection open until cancellation. The
        // post-handshake RPC dispatcher (row #9) and the events
        // subscription loop (row #10) replace this Delay with the
        // real per-pipe loops; until then we keep the pipe alive
        // so callers can observe that the handshake succeeded
        // without the engine immediately tearing the connection down.
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected on shutdown.
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

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "LifecycleService bound four pipes for workspace '{WorkspaceHash}' instance {InstanceId:D}.")]
    private static partial void LogStarted(ILogger logger, WorkspaceHash workspaceHash, Guid instanceId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "LifecycleService stopped; all pipe accept loops have drained.")]
    private static partial void LogStopped(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug,
        Message = "Accepted connection on '{Kind}' endpoint.")]
    private static partial void LogConnectionAccepted(ILogger logger, EndpointKind kind);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error,
        Message = "Accept loop for '{Kind}' endpoint faulted; the engine will shut down.")]
    private static partial void LogAcceptLoopFaulted(ILogger logger, Exception exception, EndpointKind kind);
}
