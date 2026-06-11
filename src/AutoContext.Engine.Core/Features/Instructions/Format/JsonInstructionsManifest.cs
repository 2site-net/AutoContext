namespace AutoContext.Engine.Core.Features.Instructions.Format;

/// <summary>
/// Disk read-model for the generated <c>instructions-manifest.json</c>
/// fact index — the join spine the engine reads at startup. Mirrors the
/// generator's <c>JsonInstructionsManifest</c> writer: a schema version
/// plus one <see cref="JsonInstructionsManifestEntry"/> per corpus file
/// (including the always-attached ones), in document order. Carries only
/// machine-extracted facts; curatorial data (label, categories,
/// always-attached membership) lives in
/// <see cref="JsonInstructionsCatalog"/>.
/// </summary>
/// <param name="SchemaVersion">The side-car schema version.</param>
/// <param name="Instructions">The per-file fact rows, in document
/// order.</param>
internal sealed record JsonInstructionsManifest(
    string? SchemaVersion = null,
    IReadOnlyList<JsonInstructionsManifestEntry>? Instructions = null);
