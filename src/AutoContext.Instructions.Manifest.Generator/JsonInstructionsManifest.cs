namespace AutoContext.Instructions.Manifest.Generator;

using System.Text.Json.Serialization;

/// <summary>
/// The build-generated <c>instructions-manifest.json</c> envelope: a schema
/// version and the per-file fact rows the engine merges with the hand-authored
/// <see cref="JsonInstructionsCatalog"/> at startup. The manifest is one of the
/// three decoupled representations (P3): it carries only machine-extracted facts
/// about each corpus file, never the curatorial taxonomy (categories, label,
/// activation flags) and never workspace-state fields resolved per request.
/// </summary>
internal sealed class JsonInstructionsManifest(
    string schemaVersion,
    IReadOnlyList<JsonInstructionsManifestEntry> instructions)
{
    /// <summary>Gets the per-file fact rows, ordered by <see cref="JsonInstructionsManifestEntry.Key"/>.</summary>
    [JsonPropertyOrder(1)]
    public IReadOnlyList<JsonInstructionsManifestEntry> Instructions { get; } = instructions;

    /// <summary>Gets the manifest schema version.</summary>
    [JsonPropertyOrder(0)]
    public string SchemaVersion { get; } = schemaVersion;
}
