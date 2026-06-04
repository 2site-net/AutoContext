namespace AutoContext.Instructions.Manifest.Generator;

using System.Text.Json.Serialization;

/// <summary>
/// The wire-shape <c>instructions-files.json</c> envelope: a schema version and
/// the catalogue rows the engine serves through <c>Instructions.List</c>.
/// </summary>
internal sealed class InstructionsManifest(
    string schemaVersion,
    IReadOnlyList<InstructionsManifestEntry> instructions)
{
    /// <summary>Gets the catalogue rows, ordered by <see cref="InstructionsManifestEntry.Key"/>.</summary>
    [JsonPropertyOrder(1)]
    public IReadOnlyList<InstructionsManifestEntry> Instructions { get; } = instructions;

    /// <summary>Gets the manifest schema version.</summary>
    [JsonPropertyOrder(0)]
    public string SchemaVersion { get; } = schemaVersion;
}
