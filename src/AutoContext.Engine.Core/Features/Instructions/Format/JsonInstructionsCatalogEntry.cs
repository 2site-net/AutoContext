namespace AutoContext.Engine.Core.Features.Instructions.Format;

/// <summary>
/// One curatorial row of <c>instructions-catalog.json</c>, keyed by
/// <see cref="FileName"/> onto the manifest fact spine. Mirrors the
/// generator's <c>JsonInstructionsCatalogEntry</c> shape. Carries the UI
/// <see cref="Label"/>, the <see cref="Category"/> membership the file
/// belongs to, and the engine-internal <see cref="ActivationFlags"/> the
/// workspace-context evaluator reads — the flags are never serialized to
/// the wire.
/// </summary>
/// <param name="Label">The human-friendly display label.</param>
/// <param name="FileName">The corpus file name including the
/// <c>.instructions.md</c> extension; the join key onto the
/// manifest.</param>
/// <param name="Category">The category name this file belongs to;
/// must resolve to a declared
/// <see cref="JsonInstructionsCatalogCategory"/>.</param>
/// <param name="ActivationFlags">The engine-internal workspace-context
/// flags that gate activation, or <see langword="null"/> when the file
/// is unconditional.</param>
internal sealed record JsonInstructionsCatalogEntry(
    string? Label = null,
    string? FileName = null,
    string? Category = null,
    IReadOnlyList<string>? ActivationFlags = null);
