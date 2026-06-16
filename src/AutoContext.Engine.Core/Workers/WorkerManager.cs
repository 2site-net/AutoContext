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
/// more than one process per worker id. The ready barrier is the
/// worker's stderr ready marker (see <see cref="WorkerProcessInfo.ReadyMarker"/>);
/// the gate completes when that line arrives and faults if the process
/// fails to start or exits before emitting it.
/// </para>
/// <para>
/// Per-worker state is guarded by a single <see cref="Lock"/>. The
/// launcher itself runs outside that lock so a process that raises
/// stderr/exit notifications during start-up cannot re-enter the gate.
/// Stderr-line and exit notifications arrive on arbitrary threads and a
/// per-spawn <c>generation</c> stamp lets a stale notification from a
/// replaced process be ignored rather than resolving or clearing the
/// slot's current spawn.
/// </para>
/// </remarks>
internal sealed partial class WorkerManager : IDisposable
{
    private bool _disposed;
    private readonly Lock _gate = new();
    private readonly IProcessLauncher<WorkerProcessInfo> _launcher;
    private readonly ILogger<WorkerManager> _logger;
    private readonly Dictionary<string, WorkerSlot> _slots;

    /// <summary>
    /// Creates a new <see cref="WorkerManager"/> over the resolved
    /// <paramref name="processInfos"/>.
    /// </summary>
    /// <param name="processInfos">The resolved launch specifications, one
    /// per worker; ids must be unique.</param>
    /// <param name="launcher">The process-creation seam.</param>
    /// <param name="logger">Diagnostic sink.</param>
    /// <exception cref="ArgumentNullException">Any argument, or any
    /// element of <paramref name="processInfos"/>, is
    /// <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Two entries share a
    /// worker id.</exception>
    public WorkerManager(
        IEnumerable<WorkerProcessInfo> processInfos,
        IProcessLauncher<WorkerProcessInfo> launcher,
        ILogger<WorkerManager> logger)
    {
        ArgumentNullException.ThrowIfNull(processInfos);
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(logger);

        _launcher = launcher;
        _logger = logger;
        _slots = new Dictionary<string, WorkerSlot>(StringComparer.Ordinal);

        foreach (var processInfo in processInfos)
        {
            ArgumentNullException.ThrowIfNull(processInfo);

            if (!_slots.TryAdd(processInfo.WorkerId, new WorkerSlot(processInfo)))
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
        List<TaskCompletionSource> waiters;
        List<IProcess> processes;

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            waiters = [];
            processes = [];

            foreach (var slot in _slots.Values)
            {
                // Invalidate in-flight notifications from any live process.
                slot.Generation++;

                if (slot.Resolver is { } resolver)
                {
                    waiters.Add(resolver);
                    slot.Resolver = null;
                }

                if (slot.Process is { } process)
                {
                    processes.Add(process);
                    slot.Process = null;
                }

                slot.ReadyTask = null;
            }
        }

        foreach (var waiter in waiters)
        {
            waiter.TrySetException(new ObjectDisposedException(nameof(WorkerManager)));
        }

        foreach (var process in processes)
        {
            process.Kill();
            process.Dispose();
        }
    }

    /// <summary>
    /// Ensures the worker identified by <paramref name="workerId"/> is
    /// running and has emitted its ready marker. Concurrent callers
    /// coalesce onto the same in-flight spawn; after a worker has exited
    /// the next call respawns it.
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

        WorkerSlot slot;
        SlotObserver? observer = null;
        var generation = 0;
        Task readyTask;

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!_slots.TryGetValue(workerId, out var resolvedSlot))
            {
                throw new InvalidOperationException(
                    $"No worker registered with id '{workerId}'.");
            }

            slot = resolvedSlot;

            if (slot.ReadyTask is null)
            {
                var resolver = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                slot.Resolver = resolver;
                slot.ReadyTask = resolver.Task;
                generation = ++slot.Generation;
                observer = new SlotObserver(this, slot, generation);
            }

            readyTask = slot.ReadyTask;
        }

        if (observer is not null)
        {
            // Start the process outside the lock: the launcher is a pluggable
            // seam and a started process can raise stderr/exit notifications
            // immediately, so holding the gate across the call would risk
            // re-entrancy and stall unrelated workers. Only the caller that
            // claimed an idle slot reaches here; coalescing callers fall
            // through to the shared ready task.
            LaunchWorker(slot, generation, observer);
        }

        return readyTask.WaitAsync(cancellationToken);
    }

    private static string FormatExitCode(int? exitCode)
        => exitCode?.ToString(CultureInfo.InvariantCulture) ?? "unknown";

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug,
        Message = "Worker '{WorkerId}' ready marker received.")]
    private static partial void LogReadyMarkerReceived(ILogger logger, string workerId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning,
        Message = "Failed to spawn worker '{WorkerId}'.")]
    private static partial void LogSpawnFailed(ILogger logger, string workerId, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
        Message = "Spawned worker '{WorkerId}' (pid {ProcessId}); waiting for ready marker.")]
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

    private void HandleExited(WorkerSlot slot, int generation, int? exitCode)
    {
        TaskCompletionSource? resolver;
        IProcess? process;

        lock (_gate)
        {
            if (slot.Generation != generation)
            {
                return;
            }

            resolver = slot.Resolver;
            slot.Resolver = null;
            process = slot.Process;
            slot.Process = null;
            slot.ReadyTask = null;
        }

        LogWorkerExited(_logger, slot.ProcessInfo.WorkerId, exitCode);

        resolver?.TrySetException(new ProcessLaunchException<WorkerProcessInfo>(
            slot.ProcessInfo,
            $"Worker '{slot.ProcessInfo.WorkerId}' exited with code {FormatExitCode(exitCode)} before becoming ready."));

        process?.Dispose();
    }

    private void HandleStandardErrorLine(WorkerSlot slot, int generation, string line)
    {
        LogWorkerStandardError(_logger, slot.ProcessInfo.WorkerId, line);

        TaskCompletionSource resolver;

        lock (_gate)
        {
            if (slot.Generation != generation || slot.Resolver is null)
            {
                return;
            }

            if (!string.Equals(line, slot.ProcessInfo.ReadyMarker, StringComparison.Ordinal))
            {
                return;
            }

            resolver = slot.Resolver;
            slot.Resolver = null;
        }

        LogReadyMarkerReceived(_logger, slot.ProcessInfo.WorkerId);
        resolver.TrySetResult();
    }

    private void LaunchWorker(WorkerSlot slot, int generation, IProcessObserver observer)
    {
        LogSpawning(_logger, slot.ProcessInfo.WorkerId, slot.ProcessInfo.Command);

        IProcess process;

        try
        {
            process = _launcher.Launch(slot.ProcessInfo, observer);
        }
        catch (ProcessLaunchException<WorkerProcessInfo> exception)
        {
            TaskCompletionSource? resolver = null;

            lock (_gate)
            {
                // Roll the slot back only if this spawn is still its current
                // one; a newer spawn or Dispose may have superseded it while
                // the gate was released.
                if (slot.Generation == generation)
                {
                    resolver = slot.Resolver;
                    slot.Resolver = null;
                    slot.ReadyTask = null;
                    slot.Process = null;
                }
            }

            LogSpawnFailed(_logger, slot.ProcessInfo.WorkerId, exception);
            resolver?.TrySetException(exception);

            return;
        }

        bool adopted;

        lock (_gate)
        {
            // While the gate was released a ready or exit notification for
            // this spawn may already have run. Adopt the handle only if this
            // spawn is still the slot's active generation and has not already
            // exited; otherwise the process is orphaned and must be torn down.
            adopted = slot.Generation == generation && slot.ReadyTask is not null;

            if (adopted)
            {
                slot.Process = process;
            }
        }

        if (adopted)
        {
            LogSpawned(_logger, slot.ProcessInfo.WorkerId, process.ProcessId);
        }
        else
        {
            process.Kill();
            process.Dispose();
        }
    }

    private sealed class SlotObserver(WorkerManager manager, WorkerSlot slot, int generation)
        : IProcessObserver
    {
        /// <inheritdoc/>
        public void OnExited(int? exitCode)
            => manager.HandleExited(slot, generation, exitCode);

        /// <inheritdoc/>
        public void OnStandardErrorLine(string line)
            => manager.HandleStandardErrorLine(slot, generation, line);
    }

    private sealed class WorkerSlot(WorkerProcessInfo processInfo)
    {
        /// <summary>The per-spawn stamp that invalidates stale notifications.</summary>
        public int Generation { get; set; }

        /// <summary>The current running process, or <see langword="null"/>.</summary>
        public IProcess? Process { get; set; }

        /// <summary>The immutable launch specification for this worker.</summary>
        public WorkerProcessInfo ProcessInfo { get; } = processInfo;

        /// <summary>The in-flight ready task, or <see langword="null"/> when idle.</summary>
        public Task? ReadyTask { get; set; }

        /// <summary>The source completed when the worker becomes ready.</summary>
        public TaskCompletionSource? Resolver { get; set; }
    }
}
