namespace AutoContext.Mcp.Server.Workers.Protocol;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// One per-task pipe request. McpToolClient opens one connection per
/// task in a tool invocation and writes exactly this envelope.
/// </summary>
public sealed record TaskRequest
{
    [JsonPropertyName("mcpTask")]
    public required string McpTask { get; init; }

    [JsonPropertyName("data")]
    public required JsonElement Data { get; init; }

    [JsonPropertyName("editorconfig")]
    public required IReadOnlyDictionary<string, string> EditorConfig { get; init; }
}
