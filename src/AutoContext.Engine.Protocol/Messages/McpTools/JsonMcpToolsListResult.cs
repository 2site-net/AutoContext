namespace AutoContext.Engine.Protocol.Messages.McpTools;

using System.Text.Json.Serialization;

/// <summary>
/// Result of the <see cref="McpToolsMethods.List"/> request: the MCP
/// tool catalog as identity rows.
/// </summary>
public sealed record JsonMcpToolsListResult
{
    /// <summary>
    /// Every catalog tool as an identity row, including disabled tools
    /// (which carry <c>disabled: true</c>) so a tree view can render
    /// the toggle UI.
    /// </summary>
    [JsonPropertyName("tools")]
    public IReadOnlyList<JsonMcpToolsListRow> Tools { get; init; } = [];
}
