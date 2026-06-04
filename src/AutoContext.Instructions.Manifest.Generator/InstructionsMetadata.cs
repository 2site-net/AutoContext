namespace AutoContext.Instructions.Manifest.Generator;

using System.Text.Json.Serialization;

/// <summary>
/// The catalogue-only <c>instructions-files-metadata.json</c> envelope: a schema
/// version and the per-file metadata rows. It is the section-indexed companion of
/// the wire-shape <see cref="InstructionsManifest"/>, derived from the same corpus
/// scan so the two catalogues describe an identical file set.
/// </summary>
internal sealed class InstructionsMetadata(
    string schemaVersion,
    IReadOnlyList<InstructionsMetadataEntry> instructions)
{
    /// <summary>Gets the metadata rows, ordered by <see cref="InstructionsMetadataEntry.Key"/>.</summary>
    [JsonPropertyOrder(1)]
    public IReadOnlyList<InstructionsMetadataEntry> Instructions { get; } = instructions;

    /// <summary>Gets the manifest schema version.</summary>
    [JsonPropertyOrder(0)]
    public string SchemaVersion { get; } = schemaVersion;
}
