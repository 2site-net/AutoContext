namespace AutoContext.Engine.Core.Workspace.Config;

using AutoContext.Engine.Core.Workspace.Config.Snapshot;

/// <summary>
/// Applies a single pure edit to the current config snapshot and
/// publishes the result when it differs.
/// </summary>
internal interface IConfigUpdater
{
    /// <summary>
    /// Applies <paramref name="edit"/> to the current snapshot. Returning
    /// the same snapshot instance is treated as a no-op; any other result
    /// is published.
    /// </summary>
    /// <param name="edit">Pure transform of the current snapshot.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task UpdateAsync(
        Func<ConfigSnapshot, ConfigSnapshot> edit,
        CancellationToken cancellationToken = default);
}
