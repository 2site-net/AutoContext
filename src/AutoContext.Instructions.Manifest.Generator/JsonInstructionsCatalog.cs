namespace AutoContext.Instructions.Manifest.Generator;

using System.Text.Json.Serialization;

/// <summary>
/// The hand-authored <c>instructions-catalog.json</c> envelope: the curatorial
/// taxonomy that decorates the corpus for presentation. It is one of the three
/// decoupled representations (P3) — authored and version-controlled by hand,
/// never generated — and the generator reads it only to cross-validate it against
/// the corpus (every cataloged file exists; every non-always-attached corpus
/// file is cataloged; every category membership resolves). The engine merges it
/// with the generated <see cref="JsonInstructionsManifest"/> at startup.
/// </summary>
internal sealed class JsonInstructionsCatalog(
    string schemaVersion,
    IReadOnlyList<string> alwaysAttached,
    IReadOnlyList<JsonInstructionsCatalogCategory> categories,
    IReadOnlyList<JsonInstructionsCatalogEntry> instructions)
{
    /// <summary>
    /// Gets the corpus file names the engine injects into every turn
    /// unconditionally. These are deliberately omitted from <see cref="Instructions"/>
    /// (they are not user-facing curated entries); declaring them here is the single
    /// source of truth for the always-attached set, replacing the former hard-coded
    /// generator list.
    /// </summary>
    [JsonPropertyOrder(1)]
    public IReadOnlyList<string> AlwaysAttached { get; } = alwaysAttached;

    /// <summary>Gets the declared categories, in presentation order.</summary>
    [JsonPropertyOrder(2)]
    public IReadOnlyList<JsonInstructionsCatalogCategory> Categories { get; } = categories;

    /// <summary>Gets the curated per-file entries.</summary>
    [JsonPropertyOrder(3)]
    public IReadOnlyList<JsonInstructionsCatalogEntry> Instructions { get; } = instructions;

    /// <summary>Gets the catalog schema version.</summary>
    [JsonPropertyOrder(0)]
    public string SchemaVersion { get; } = schemaVersion;
}
