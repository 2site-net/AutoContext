namespace AutoContext.Engine.Core.Workspace.Config;

using AutoContext.Engine.Core.Workspace.Config.Snapshot;

/// <summary>
/// Read-only seam over the in-memory config snapshot. Decouples
/// snapshot readers — the <c>Config.Get</c> RPC handler — from the
/// stateful <see cref="ConfigFileManager"/> so they depend
/// only on the ability to read the current value, not on the
/// manager's write/watch surface.
/// </summary>
internal interface IConfigSnapshotAccessor
{
    /// <summary>
    /// The config snapshot currently held in memory. Each read
    /// returns an immutable value that is safe to use without
    /// locking.
    /// </summary>
    ConfigSnapshot Current { get; }
}
