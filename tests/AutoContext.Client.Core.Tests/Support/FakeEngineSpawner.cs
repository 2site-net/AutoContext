namespace AutoContext.Client.Core.Tests.Support;

using AutoContext.Client.Core.Engine;

/// <summary>
/// Test <see cref="IEngineSpawner"/> that records how many times it was
/// asked to spawn and runs a caller-supplied callback each time — used
/// to stand a <see cref="FakeEnginePipeServer"/> up on demand so the
/// resolver's cold-retry loop can then connect.
/// </summary>
internal sealed class FakeEngineSpawner(
    Func<EngineSpawnRequest, CancellationToken, Task>? onSpawn = null) : IEngineSpawner
{
    private readonly Func<EngineSpawnRequest, CancellationToken, Task> _onSpawn
        = onSpawn ?? ((_, _) => Task.CompletedTask);
    private int _spawnCount;

    /// <summary>Number of times <see cref="SpawnAsync"/> was invoked.</summary>
    public int SpawnCount => Volatile.Read(ref _spawnCount);

    /// <summary>The most recent request passed to <see cref="SpawnAsync"/>.</summary>
    public EngineSpawnRequest? LastRequest { get; private set; }

    public async Task SpawnAsync(EngineSpawnRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Interlocked.Increment(ref _spawnCount);
        LastRequest = request;
        await _onSpawn(request, cancellationToken).ConfigureAwait(false);
    }
}
