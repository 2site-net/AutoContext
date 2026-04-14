namespace AutoContext.Mcp.Shared.McpTools;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

/// <summary>
/// Response containing resolved tool modes and optional EditorConfig data.
/// </summary>
/// <param name="Tools">Per-tool enabled/disabled status.</param>
/// <param name="EditorConfig">Resolved EditorConfig properties, or <see langword="null" /> when no keys were requested.</param>
internal sealed record McpToolsResponse(
    Dictionary<string, bool> Tools,
    [property: JsonPropertyName("editorconfig")] Dictionary<string, string>? EditorConfig = null)
{
    /// <summary>Gets the response type discriminator.</summary>
    [SuppressMessage("Performance", "CA1822",
        Justification = "Must be an instance property for JSON serialization.")]
    public string Type => "mcp-tools";
}
