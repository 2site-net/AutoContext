namespace AutoContext.Engine.Core.Tests.Support.Workspace.Config;

using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;

/// <summary>
/// <see cref="IConfigUpdater"/> test double whose
/// <see cref="UpdateAsync"/> always faults with the supplied
/// exception, letting tests drive the <c>Config.Toggle*</c> publish
/// failure path (the handler's <c>InternalError</c> reply).
/// </summary>
internal sealed class ThrowingConfigUpdater(Exception failure) : IConfigUpdater
{
    public Task UpdateAsync(
        Func<ConfigSnapshot, ConfigSnapshot> edit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(edit);

        return Task.FromException(failure);
    }
}
