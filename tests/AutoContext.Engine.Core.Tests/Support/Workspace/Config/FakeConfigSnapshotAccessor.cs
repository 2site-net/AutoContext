namespace AutoContext.Engine.Core.Tests.Support.Workspace.Config;

using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;

/// <summary>
/// Trivial <see cref="IConfigSnapshotAccessor"/> test double that
/// returns a fixed snapshot, letting tests drive the
/// <c>Config.Get</c> RPC path without spinning up a stateful
/// <see cref="ConfigFileManager"/> (no temp directory, no
/// file watcher, nothing to dispose).
/// </summary>
internal sealed class FakeConfigSnapshotAccessor : IConfigSnapshotAccessor
{
    public ConfigSnapshot Current { get; init; } = ConfigSnapshot.Empty;
}
