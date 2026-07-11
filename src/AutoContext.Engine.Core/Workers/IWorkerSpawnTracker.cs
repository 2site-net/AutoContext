namespace AutoContext.Engine.Core.Workers;

/// <summary>
/// Read-only view over which workers the current engine has ever
/// spawned. Backs the <c>Logs.GetWorker</c> / <c>Logs.TailWorker</c>
/// not-found decision: a worker the engine has never spawned is
/// reported <c>not-found</c>, distinct from a spawned-but-quiet
/// worker whose empty log surfaces as an <c>ok</c> result.
/// </summary>
internal interface IWorkerSpawnTracker
{
    /// <summary>
    /// Reports whether the engine has ever spawned the worker
    /// identified by <paramref name="workerId"/> during this
    /// process's lifetime.
    /// </summary>
    /// <param name="workerId">The worker's short id.</param>
    /// <returns><see langword="true"/> once the worker has been
    /// spawned at least once; <see langword="false"/> for an unknown
    /// id or a registered worker that has never been started.</returns>
    /// <exception cref="ArgumentException"><paramref name="workerId"/>
    /// is <see langword="null"/> or empty.</exception>
    bool HasEverSpawned(string workerId);
}
