namespace AutoContext.Engine.Core.Features.Instructions;

using AutoContext.Engine.Core.Features.Instructions.Snapshot;

/// <summary>
/// Read-only seam over the in-memory instruction corpus snapshot.
/// Decouples snapshot readers — the <c>Instructions.*</c> RPC handlers —
/// from the stateful <see cref="InstructionsManifestService"/> so they
/// depend only on the ability to read the current corpus, not on its
/// hosted-service lifecycle.
/// </summary>
internal interface IInstructionsManifestAccessor
{
    /// <summary>
    /// The corpus snapshot currently held in memory. Each read returns
    /// an immutable value that is safe to use without locking. Before
    /// the startup load completes this is
    /// <see cref="InstructionsManifestSnapshot.Empty"/>.
    /// </summary>
    InstructionsManifestSnapshot Current { get; }
}
