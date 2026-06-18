namespace AutoContext.Engine.Core.Features.Instructions.Snapshot;

/// <summary>
/// Immutable category definition from the instruction catalog: the
/// bucket <see cref="Name"/> a file's membership references and the
/// human-readable <see cref="Description"/>. Carried on the
/// <see cref="InstructionsManifestSnapshot"/> taxonomy and surfaced
/// verbatim by the <c>Instructions.Categories</c> RPC.
/// </summary>
internal sealed record InstructionsCategoryEntry
{
    /// <summary>
    /// The category name; the value a file's category membership
    /// resolves against.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The human-readable category description.
    /// </summary>
    public required string Description { get; init; }
}
