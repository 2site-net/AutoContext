namespace AutoContext.Client.Core.Engine;

/// <summary>
/// Immutable description of the engine process the resolver launches
/// on cold start. Carries the launcher identity the engine binds its
/// endpoints under, the already-resolved binary to launch, and the
/// optional idle-timeout to forward. A spawner turns this into a
/// detached <c>autocontext-engine</c> process; the resolver's
/// connect-retry loop, not the spawner, observes readiness.
/// </summary>
/// <param name="WorkspacePath">Absolute workspace path passed on
/// <c>--workspace</c>.</param>
/// <param name="InstanceId">Launcher-minted UUIDv4 passed on
/// <c>--instance-id</c>.</param>
/// <param name="InstanceLabel">Human-readable label passed on
/// <c>--instance-label</c> when non-empty; omitted when empty.</param>
/// <param name="IdleTimeout">Idle-timeout forwarded on
/// <c>--idle-timeout</c> when set; omitted when
/// <see langword="null"/>.</param>
/// <param name="EngineBinaryPath">Absolute path of the
/// <c>autocontext-engine</c> binary to launch.</param>
public sealed record EngineSpawnRequest(
    string WorkspacePath,
    Guid InstanceId,
    string InstanceLabel,
    TimeSpan? IdleTimeout,
    string EngineBinaryPath);
