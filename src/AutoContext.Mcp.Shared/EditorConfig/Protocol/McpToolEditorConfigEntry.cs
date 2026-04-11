namespace AutoContext.Mcp.Shared.EditorConfig.Protocol;

using System.Text.Json.Serialization;

/// <summary>
/// A single MCP tool included in a <see cref="McpToolsRequest"/>.
/// </summary>
/// <param name="Name">Tool name as registered in the MCP server.</param>
/// <param name="EditorConfigKeys">Optional EditorConfig keys the tool depends on.</param>
internal sealed record McpToolEditorConfigEntry(
    string Name,
    [property: JsonPropertyName("editorconfig-keys")] string[]? EditorConfigKeys = null);
