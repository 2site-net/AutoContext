namespace AutoContext.Engine.Core.Tests.Support.Workspace.Config;

using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;

/// <summary>
/// In-memory <see cref="IConfigSnapshotAccessor"/> /
/// <see cref="IConfigUpdater"/> / <see cref="IConfigChangeNotifier"/>
/// test double that holds a snapshot and applies edits to it, letting
/// tests drive the <c>Config.Get</c> and <c>Config.Toggle*</c> RPC
/// paths without spinning up a stateful <see cref="ConfigFileManager"/>
/// (no temp directory, no file watcher, nothing to dispose).
/// Read-after-write is coherent: an edit applied through
/// <see cref="UpdateAsync"/> is visible on the next
/// <see cref="Current"/> read, and raises <see cref="Changed"/> when
/// the snapshot differs (mirroring the real manager).
/// </summary>
internal sealed class FakeConfigSnapshotAccessor : IConfigSnapshotAccessor, IConfigUpdater, IConfigChangeNotifier
{
    public event EventHandler<ConfigSnapshot>? Changed;

    public ConfigSnapshot Current { get; set; } = ConfigSnapshot.Empty;

    public Task UpdateAsync(
        Func<ConfigSnapshot, ConfigSnapshot> edit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(edit);

        var next = edit(Current);
        if (!ReferenceEquals(next, Current))
        {
            Current = next;
            Changed?.Invoke(this, next);
        }

        return Task.CompletedTask;
    }
}
