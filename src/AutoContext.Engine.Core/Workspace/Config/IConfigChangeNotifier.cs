namespace AutoContext.Engine.Core.Workspace.Config;

using AutoContext.Engine.Core.Workspace.Config.Snapshot;

/// <summary>
/// Change-notification seam over the in-memory config snapshot.
/// Decouples readers that must react to config edits — the
/// <c>Instructions.Subscribe</c> rebroadcast bridge — from the
/// stateful <see cref="ConfigFileManager"/>, so they depend only on
/// the ability to observe changes, not on the manager's write/watch
/// surface.
/// </summary>
internal interface IConfigChangeNotifier
{
    /// <summary>
    /// Raised after a new snapshot is published — by a
    /// <c>Config.Toggle*</c> edit or a watcher-driven reconciliation
    /// — carrying the snapshot now in effect. Not raised by the
    /// initial disk load.
    /// </summary>
    event EventHandler<ConfigSnapshot>? Changed;
}
