namespace AutoContext.Engine.Core.Workspace.Config.Format;

using System.Text.Json.Serialization;

using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;

/// <summary>
/// Immutable in-memory model of a parsed <c>.autocontext.json</c>
/// file: the engine deserialises the file into this record, reads from
/// it, and serialises it back unchanged. It is pure data — the wire
/// boundary that <see cref="JsonConfigFileExtensions.ToDomainGraph"/>
/// and <see cref="ConfigSnapshotExtensions.ToFileFormat"/> map to and from
/// the <see cref="ConfigSnapshot"/> domain graph.
/// </summary>
/// <remarks>
/// The parameter order (<c>version</c>, <c>diagnostic</c>,
/// <c>instructions</c>, <c>mcpTools</c>) is the order keys are written
/// to disk; keep it stable so saved files stay byte-for-byte stable.
/// Each optional section is <see langword="null"/> (never an empty
/// map) when absent, so the serializer leaves it out entirely.
/// </remarks>
/// <param name="Version">Full semver of the engine build that last
/// wrote the file. Informational on load; overwritten with the
/// current engine version on every save.</param>
/// <param name="Diagnostic">Optional diagnostic preferences, carried
/// through verbatim.</param>
/// <param name="Instructions">Per-instruction-file state keyed by
/// file name. Insertion order is preserved on save.</param>
/// <param name="McpTools">Per-MCP-tool state keyed by tool name. Each
/// value is either the shorthand <c>false</c> or an object entry.
/// Insertion order is preserved on save.</param>
internal sealed record JsonConfigFile(
    [property: JsonPropertyName("version")] string? Version = null,
    [property: JsonPropertyName("diagnostic")] JsonConfigFileDiagnostic? Diagnostic = null,
    [property: JsonPropertyName("instructions")] IReadOnlyDictionary<string, JsonConfigFileInstructionsEntry>? Instructions = null,
    [property: JsonPropertyName("mcpTools")] IReadOnlyDictionary<string, JsonConfigFileMcpToolValue>? McpTools = null)
{
    /// <summary>
    /// The shared empty config: no version and no sections.
    /// </summary>
    public static JsonConfigFile Empty { get; } = new();

    /// <summary>
    /// <see langword="true"/> when no section carries state, meaning
    /// the file should be deleted rather than written.
    /// </summary>
    [JsonIgnore]
    public bool IsEmpty
        => Diagnostic is null && Instructions is null && McpTools is null;
}
