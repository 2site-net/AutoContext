namespace AutoContext.Engine.Core.Features.Instructions;

using AutoContext.Engine.Core.Features.Instructions.Snapshot;

/// <summary>
/// Read-only seam over the in-memory workspace override inventory.
/// Decouples override readers — chiefly <see cref="InstructionsBodyProjector"/>,
/// which prefers a workspace-local copy over the bundled body — from the
/// stateful <see cref="InstructionsOverridesWatcher"/> so they depend only
/// on the ability to read the current inventory, not on its watcher
/// lifecycle.
/// </summary>
internal interface IInstructionsOverridesAccessor
{
    /// <summary>
    /// The override inventory currently held in memory. Each read returns
    /// an immutable value that is safe to use without locking. Before the
    /// initial scan completes this is
    /// <see cref="InstructionsOverridesSnapshot.Empty"/>.
    /// </summary>
    InstructionsOverridesSnapshot Current { get; }
}
