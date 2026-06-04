namespace AutoContext.Build.Tasks;

using System.Collections.Generic;

/// <summary>
/// The wire-shape <c>instructions-files.json</c> envelope: a schema version and
/// the catalogue rows the engine serves through <c>Instructions.List</c>.
/// </summary>
internal sealed class InstructionsFilesManifest(
    string schemaVersion,
    IReadOnlyList<InstructionsFileManifestEntry> instructions)
{
    /// <summary>Gets the catalogue rows, ordered by <see cref="InstructionsFileManifestEntry.Key"/>.</summary>
    public IReadOnlyList<InstructionsFileManifestEntry> Instructions { get; } = instructions;

    /// <summary>Gets the manifest schema version.</summary>
    public string SchemaVersion { get; } = schemaVersion;
}
