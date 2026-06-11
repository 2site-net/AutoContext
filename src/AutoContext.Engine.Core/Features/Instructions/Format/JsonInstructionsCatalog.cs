namespace AutoContext.Engine.Core.Features.Instructions.Format;

/// <summary>
/// Disk read-model for the hand-authored <c>instructions-catalog.json</c>
/// curatorial layer the engine reads at startup. Mirrors the generator's
/// <c>JsonInstructionsCatalog</c> shape: the <see cref="AlwaysAttached"/>
/// file-name list (the single source of truth for always-attached
/// membership), the <see cref="Categories"/> taxonomy definitions, and
/// one <see cref="JsonInstructionsCatalogEntry"/> per cataloged file. The
/// always-attached files are deliberately omitted from
/// <see cref="Instructions"/>; the loader merges this layer onto the
/// <see cref="JsonInstructionsManifest"/> fact spine by file name.
/// </summary>
/// <param name="SchemaVersion">The side-car schema version.</param>
/// <param name="AlwaysAttached">The file names that are always attached,
/// independent of workspace context.</param>
/// <param name="Categories">The category taxonomy definitions.</param>
/// <param name="Instructions">The per-file curatorial rows, in document
/// order, excluding the always-attached files.</param>
internal sealed record JsonInstructionsCatalog(
    string? SchemaVersion = null,
    IReadOnlyList<string>? AlwaysAttached = null,
    IReadOnlyList<JsonInstructionsCatalogCategory>? Categories = null,
    IReadOnlyList<JsonInstructionsCatalogEntry>? Instructions = null);
