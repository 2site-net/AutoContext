namespace AutoContext.Engine.Core.Workspace.Config;

/// <summary>
/// Reloads the workspace's <c>.autocontext.json</c> from disk into the
/// in-memory snapshot exposed through <see cref="IConfigSnapshotAccessor"/>.
/// The stdio MCP-server role reloads on every request (it binds no
/// <see cref="FileSystemWatcher"/> and keeps no subscription), so
/// the capability handlers observe the authoritative disabled state at the
/// moment the request is served.
/// </summary>
internal interface IConfigReloader
{
    /// <summary>
    /// Re-reads the config file and republishes the in-memory snapshot.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task ReloadAsync(CancellationToken cancellationToken);
}
