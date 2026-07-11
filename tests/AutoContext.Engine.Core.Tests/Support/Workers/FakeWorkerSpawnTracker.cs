namespace AutoContext.Engine.Core.Tests.Support.Workers;

using AutoContext.Engine.Core.Workers;

/// <summary>
/// Configurable <see cref="IWorkerSpawnTracker"/> test double: reports a
/// worker as ever-spawned exactly when its id is in the set supplied at
/// construction. An empty set models an engine that has spawned nothing yet.
/// </summary>
internal sealed class FakeWorkerSpawnTracker(params string[] spawnedWorkerIds) : IWorkerSpawnTracker
{
    private readonly HashSet<string> _spawned = new(spawnedWorkerIds, StringComparer.Ordinal);

    /// <inheritdoc/>
    public bool HasEverSpawned(string workerId)
    {
        ArgumentException.ThrowIfNullOrEmpty(workerId);
        return _spawned.Contains(workerId);
    }
}
