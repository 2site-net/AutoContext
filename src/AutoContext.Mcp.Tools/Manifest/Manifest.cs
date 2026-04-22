namespace AutoContext.Mcp.Tools.Manifest;

using System.Text.Json.Serialization;

/// <summary>
/// Strongly-typed representation of <c>.mcp-tools.json</c>.
/// </summary>
[JsonConverter(typeof(ManifestJsonConverter))]
public sealed record Manifest
{
    /// <summary>
    /// Manifest format version (currently <c>"1"</c>).
    /// </summary>
    public required string SchemaVersion { get; init; }

    /// <summary>
    /// Worker name (e.g. <c>"dotnet"</c>, <c>"workspace"</c>, <c>"web"</c>) →
    /// the groups that worker owns.
    /// </summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<ManifestGroup>> Workers { get; init; }
}
