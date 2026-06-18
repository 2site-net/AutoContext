namespace AutoContext.Engine.Core.Workers;

using System.Globalization;

using AutoContext.Engine.Core.Infrastructure.Diagnostics;

using Microsoft.Extensions.Logging;

/// <summary>
/// Owns the lifecycle of the engine's worker processes behind a single
/// lazy <see cref="EnsureRunningAsync"/> gate. There is no eager-start
/// step: a worker is spawned on demand the first time a caller ensures
/// it is running, and respawned automatically after a previous process
/// exits.
/// </summary>
/// <remarks>
/// <para>
/// Concurrent callers for the same worker coalesce onto a single
/// in-flight ready <see cref="Task"/>, so a burst of calls never starts
/// more than one process per worker id. The ready barrier is a successful
/// connection to the worker's listen endpoint: the gate completes once the
/// readiness probe connects and faults if the process fails to start or
/// exits before becoming connectable.
/// </para>
/// <para>
/// Each worker has its own guarded <see cref="WorkerProcessHost"/>. The
/// launcher runs outside that host's gate so a process that raises
/// stderr/exit notifications during start-up cannot re-enter the gate.
/// Stderr-line and exit notifications arrive on arbitrary threads; each
/// concrete spawn is represented by its own
/// <see cref="WorkerProcessInstance"/>, and a notification acts only while
/// that instance is still its host's current one, so a stale notification
/// from a replaced process is ignored.
/// </para>
/// </remarks>
internal sealed partial class WorkerManager : IDisposable
{
    private bool _disposed;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, WorkerProcessHost> _hosts;

    /// <summary>
    /// Creates a new <see cref="WorkerManager"/> over the resolved
    /// <paramref name="workersProcessInfo"/>.
    /// </summary>
    /// <param name="workersProcessInfo">The resolved launch specifications,
    /// one per worker; ids must be unique.</param>
    /// <param name="launcher">The process-creation seam.</param>
    /// <param name="probe">The readiness seam that confirms a spawned
    /// worker is connectable.</param>
    /// <param name="logger">Diagnostic sink.</param>
    /// <exception cref="ArgumentNullException">Any argument, or any element
    /// of <paramref name="workersProcessInfo"/>, is
    /// <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Two entries share a
    /// worker id.</exception>
    public WorkerManager(
        IEnumerable<WorkerProcessInfo> workersProcessInfo,
        IProcessLauncher<WorkerProcessInfo> launcher,
        IWorkerConnectionProbe probe,
        ILogger<WorkerManager> logger)
    {
        ArgumentNullException.ThrowIfNull(workersProcessInfo);
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(logger);

        _hosts = new Dictionary<string, WorkerProcessHost>(StringComparer.Ordinal);

        foreach (var processInfo in workersProcessInfo)
        {
            ArgumentNullException.ThrowIfNull(processInfo);

            if (!_hosts.TryAdd(
                    processInfo.WorkerId,
                    new WorkerProcessHost(processInfo, launcher, probe, logger)))
            {
                throw new InvalidOperationException(
                    $"Duplicate worker id '{processInfo.WorkerId}'.");
            }
        }
    }

    /// <summary>
    /// Rejects every pending ready waiter and terminates every running
    /// worker process. Subsequent <see cref="EnsureRunningAsync"/> calls
    /// fault with <see cref="ObjectDisposedException"/>.
    /// </summary>
    public void Dispose()
    {
        WorkerProcessHost[] hosts;

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            hosts = [.. _hosts.Values];
        }

        foreach (var host in hosts)
        {
            host.Dispose();
        }
    }

    /// <summary>
    /// Ensures the worker identified by <paramref name="workerId"/> is
    /// running and ready. Concurrent callers coalesce onto the same
    /// in-flight spawn; after a worker has exited the next call respawns it.
    /// </summary>
    /// <param name="workerId">The worker's short id.</param>
    /// <param name="cancellationToken">Cancels this caller's wait without
    /// affecting the shared spawn or sibling callers.</param>
    /// <returns>A task that completes once the worker is ready.</returns>
    /// <exception cref="ArgumentException"><paramref name="workerId"/> is
    /// <see langword="null"/> or empty.</exception>
    /// <exception cref="InvalidOperationException">No worker is registered
    /// with id <paramref name="workerId"/>.</exception>
    /// <exception cref="ObjectDisposedException">The manager has been
    /// disposed.</exception>
    public Task EnsureRunningAsync(string workerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(workerId);

        WorkerProcessHost host;

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!_hosts.TryGetValue(workerId, out var resolvedHost))
            {
                throw new InvalidOperationException(
                    $"No worker registered with id '{workerId}'.");
            }

            host = resolvedHost;
        }

        return host.EnsureRunningAsync(cancellationToken);
    }

    private static void CancelAndDispose(CancellationTokenSource? cts)
    {
        if (cts is null)
        {
            return;
        }

        cts.Cancel();
        cts.Dispose();
    }

    private static string FormatExitCode(int? exitCode)
        => exitCode?.ToString(CultureInfo.InvariantCulture) ?? "unknown";

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug,
        Message = "Worker '{WorkerId}' ready (pipe connected).")]
    private static partial void LogReady(ILogger logger, string workerId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning,
        Message = "Failed to spawn worker '{WorkerId}'.")]
    private static partial void LogSpawnFailed(ILogger logger, string workerId, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
        Message = "Spawned worker '{WorkerId}' (pid {ProcessId}); waiting for readiness.")]
    private static partial void LogSpawned(ILogger logger, string workerId, int? processId);

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Spawning worker '{WorkerId}': {Command}")]
    private static partial void LogSpawning(ILogger logger, string workerId, string command);

    [LoggerMessage(EventId = 6, Level = LogLevel.Debug,
        Message = "Worker '{WorkerId}' exited with code {ExitCode}.")]
    private static partial void LogWorkerExited(ILogger logger, string workerId, int? exitCode);

    [LoggerMessage(EventId = 5, Level = LogLevel.Debug,
        Message = "[{WorkerId}] {Line}")]
    private static partial void LogWorkerStandardError(ILogger logger, string workerId, string line);

    /// <summary>
    /// Per-worker lifecycle owner. Guards the current spawn and coalesces
    /// concurrent callers onto that spawn's ready task. The host is idle
    /// exactly when it holds no current <see cref="WorkerProcessInstance"/>.
    /// </summary>
    private sealed class WorkerProcessHost(
        WorkerProcessInfo processInfo,
        IProcessLauncher<WorkerProcessInfo> launcher,
        IWorkerConnectionProbe probe,
        ILogger logger) : IDisposable
    {
        private WorkerProcessInstance? _currentInstance;
        private bool _disposed;
        private readonly Lock _gate = new();

        /// <summary>
        /// Detaches and tears down the current spawn, if any. Idempotent.
        /// </summary>
        public void Dispose()
        {
            WorkerProcessInstance? currentInstance;

            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                currentInstance = _currentInstance;
                _currentInstance = null;
            }

            currentInstance?.Abort(new ObjectDisposedException(nameof(WorkerManager)));
        }

        /// <summary>
        /// Returns the current spawn's ready task, starting a new spawn when
        /// the host is idle. The launch runs outside the gate.
        /// </summary>
        public Task EnsureRunningAsync(CancellationToken cancellationToken)
        {
            WorkerProcessInstance? instanceToStart = null;
            Task readyTask;

            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                _currentInstance ??= instanceToStart = new WorkerProcessInstance(
                    this,
                    processInfo,
                    launcher,
                    probe,
                    logger);

                readyTask = _currentInstance.ReadyTask;
            }

            instanceToStart?.Start();

            return readyTask.WaitAsync(cancellationToken);
        }

        /// <summary>
        /// Reports whether <paramref name="instance"/> is still this host's
        /// current spawn, taking the gate to read it safely.
        /// </summary>
        public bool IsCurrent(WorkerProcessInstance instance)
        {
            lock (_gate)
            {
                return ReferenceEquals(_currentInstance, instance);
            }
        }

        /// <summary>
        /// Records the launched process against <paramref name="instance"/>
        /// and returns its readiness probe token, but only while the instance
        /// is still current. Returns <see langword="false"/> for a superseded
        /// spawn so the caller tears the orphan down.
        /// </summary>
        public bool TryAdopt(
            WorkerProcessInstance instance,
            IProcess process,
            out CancellationToken probeToken)
        {
            lock (_gate)
            {
                if (!ReferenceEquals(_currentInstance, instance))
                {
                    probeToken = default;
                    return false;
                }

                probeToken = instance.Adopt(process);
                return true;
            }
        }

        /// <summary>
        /// Hands the instance's readiness probe to the caller for disposal
        /// while leaving it current. Returns <see langword="false"/> for a
        /// superseded spawn. Used on the non-terminal ready transition.
        /// </summary>
        public bool TryDetachProbe(
            WorkerProcessInstance instance,
            out CancellationTokenSource? probe)
        {
            lock (_gate)
            {
                if (!ReferenceEquals(_currentInstance, instance))
                {
                    probe = null;
                    return false;
                }

                probe = instance.DetachProbe();
                return true;
            }
        }

        /// <summary>
        /// Clears the current spawn and hands its probe and process to the
        /// caller for teardown, but only if <paramref name="instance"/> is
        /// still current. Returns <see langword="false"/> otherwise.
        /// </summary>
        public bool TryRetire(
            WorkerProcessInstance instance,
            out CancellationTokenSource? probe,
            out IProcess? process)
        {
            lock (_gate)
            {
                if (!ReferenceEquals(_currentInstance, instance))
                {
                    probe = null;
                    process = null;
                    return false;
                }

                _currentInstance = null;
                probe = instance.DetachProbe();
                process = instance.DetachProcess();
                return true;
            }
        }
    }

    /// <summary>
    /// One concrete spawned worker process: the unit of identity that tells
    /// a live spawn from a superseded one. Owns its launch, readiness
    /// monitoring, process callbacks, and the completion of the shared ready
    /// task. A notification acts only while the instance is still its host's
    /// current one.
    /// </summary>
    private sealed class WorkerProcessInstance(
        WorkerProcessHost host,
        WorkerProcessInfo processInfo,
        IProcessLauncher<WorkerProcessInfo> launcher,
        IWorkerConnectionProbe probe,
        ILogger logger) : IProcessObserver
    {
        private CancellationTokenSource? _probeCancellationTokenSource = new();
        private IProcess? _process;
        private readonly TaskCompletionSource _resolver =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>The task that completes when this spawn becomes ready.</summary>
        public Task ReadyTask
            => _resolver.Task;

        /// <summary>
        /// Terminates the spawn after the host has detached it: cancels the
        /// readiness probe, faults the ready task, and tears down the
        /// process. Called only by the host's <see cref="WorkerProcessHost.Dispose"/>.
        /// </summary>
        public void Abort(Exception exception)
        {
            CancelAndDispose(DetachProbe());
            _resolver.TrySetException(exception);

            if (DetachProcess() is { } process)
            {
                process.Kill();
                process.Dispose();
            }
        }

        /// <summary>
        /// Records the adopted process handle and returns the readiness
        /// probe's token. Called by the host under its gate while this
        /// instance is still current.
        /// </summary>
        public CancellationToken Adopt(IProcess process)
        {
            _process = process;
            return _probeCancellationTokenSource?.Token ?? CancellationToken.None;
        }

        /// <summary>
        /// Detaches and returns the readiness probe source, leaving the
        /// instance with none. Called by the host under its gate.
        /// </summary>
        public CancellationTokenSource? DetachProbe()
        {
            var probe = _probeCancellationTokenSource;
            _probeCancellationTokenSource = null;
            return probe;
        }

        /// <summary>
        /// Detaches and returns the running process handle, leaving the
        /// instance with none. Called by the host under its gate.
        /// </summary>
        public IProcess? DetachProcess()
        {
            var process = _process;
            _process = null;
            return process;
        }

        /// <inheritdoc/>
        public void OnExited(int? exitCode)
        {
            if (!host.TryRetire(this, out var probe, out var process))
            {
                return;
            }

            CancelAndDispose(probe);
            LogWorkerExited(logger, processInfo.WorkerId, exitCode);

            // No-op once the instance is already ready: a worker that exits
            // after connecting retires its host but must not turn the already
            // completed ready task into a fault.
            _resolver.TrySetException(new ProcessLaunchException<WorkerProcessInfo>(
                processInfo,
                $"Worker '{processInfo.WorkerId}' exited with code {FormatExitCode(exitCode)} before becoming ready."));

            process?.Dispose();
        }

        /// <inheritdoc/>
        public void OnStandardErrorLine(string line)
        {
            if (!host.IsCurrent(this))
            {
                return;
            }

            LogWorkerStandardError(logger, processInfo.WorkerId, line);
        }

        /// <summary>
        /// Launches the worker process and, once adopted, starts monitoring
        /// it for readiness. Runs outside the host gate.
        /// </summary>
        public void Start()
        {
            if (!host.IsCurrent(this))
            {
                return;
            }

            LogSpawning(logger, processInfo.WorkerId, processInfo.Command);

            IProcess process;

            try
            {
                process = launcher.Launch(processInfo, this);
            }
            catch (ProcessLaunchException<WorkerProcessInfo> exception)
            {
                FailLaunch(exception);
                return;
            }

            if (host.TryAdopt(this, process, out var probeToken))
            {
                LogSpawned(logger, processInfo.WorkerId, process.ProcessId);
                _ = MonitorReadinessAsync(probeToken);
            }
            else
            {
                process.Kill();
                process.Dispose();
            }
        }

        private void FailLaunch(ProcessLaunchException<WorkerProcessInfo> exception)
        {
            if (host.TryRetire(this, out var probe, out _))
            {
                CancelAndDispose(probe);
                LogSpawnFailed(logger, processInfo.WorkerId, exception);
            }

            _resolver.TrySetException(exception);
        }

        private void FaultReady(Exception exception)
        {
            if (!host.TryRetire(this, out var probe, out var process))
            {
                return;
            }

            CancelAndDispose(probe);
            LogSpawnFailed(logger, processInfo.WorkerId, exception);
            _resolver.TrySetException(exception);

            process?.Kill();
            process?.Dispose();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031",
            Justification = "Background readiness task: an unexpected probe failure must fault the ready gate, never crash the process.")]
        private async Task MonitorReadinessAsync(CancellationToken probeToken)
        {
            try
            {
                await probe.WaitForConnectionAsync(processInfo.Endpoint, probeToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Superseded, exited before ready, or disposed; whichever path
                // cancelled the probe has already faulted or replaced the instance.
                return;
            }
            catch (Exception exception)
            {
                FaultReady(exception);
                return;
            }

            ResolveReady();
        }

        private void ResolveReady()
        {
            if (!host.TryDetachProbe(this, out var probe))
            {
                return;
            }

            // Becoming ready is not terminal: the instance stays its host's
            // current one. Only the probe is retired here so a later exit
            // notification cannot dispose it a second time.
            CancelAndDispose(probe);
            LogReady(logger, processInfo.WorkerId);
            _resolver.TrySetResult();
        }
    }
}
