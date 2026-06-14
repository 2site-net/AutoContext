namespace AutoContext.McpTools.Manifest.Generator;

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

/// <summary>
/// Serialises a <see cref="JsonMcpToolsCatalog"/> to deterministic,
/// two-space-indented JSON with a trailing newline. Layout is pinned by
/// <see cref="McpToolsManifestJsonContext"/> and the shared relaxed encoder from
/// <see cref="McpToolsManifestJsonOptions"/>, so the bytes stay stable across
/// builds.
/// </summary>
internal sealed class McpToolsManifestSerializer : IMcpToolsManifestSerializer
{
    private static readonly JsonTypeInfo<JsonMcpToolsCatalog> CatalogTypeInfo =
        (JsonTypeInfo<JsonMcpToolsCatalog>)McpToolsManifestJsonOptions.Relaxed.GetTypeInfo(typeof(JsonMcpToolsCatalog));

    /// <inheritdoc />
    public string Serialize(JsonMcpToolsCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        return JsonSerializer.Serialize(catalog, CatalogTypeInfo) + "\n";
    }
}
