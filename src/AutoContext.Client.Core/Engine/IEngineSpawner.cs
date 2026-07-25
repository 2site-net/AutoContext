namespace AutoContext.Client.Core.Engine;

/// <summary>
/// Seam over engine process creation. The production implementation
/// launches a detached <c>autocontext-engine</c> via
/// <c>Process.Start</c>; test implementations stand an engine up
/// in-process so the resolver's find-or-spawn flow can be exercised
/// without a real binary. Spawning is the only part of the client
/// that is not pure dial logic, which is why it is the client's one
/// substitutable seam.
/// </summary>
public interface IEngineSpawner
{
    /// <summary>
    /// Starts an engine for <paramref name="request"/> and returns
    /// once the launch has been issued — not once the engine is
    /// accepting connections. The caller's connect-retry loop
    /// observes readiness.
    /// </summary>
    /// <param name="request">Launch specification. Must not be
    /// <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation for the launch.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="request"/> is <see langword="null"/>.</exception>
    Task SpawnAsync(EngineSpawnRequest request, CancellationToken cancellationToken);
}
