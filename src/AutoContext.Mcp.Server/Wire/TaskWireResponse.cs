namespace AutoContext.Mcp.Server.Wire;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// One per-task pipe response. The worker always emits this exact shape
/// regardless of success or failure — <see cref="Output"/> is null on
/// error and <see cref="Error"/> is empty on success.
/// </summary>
public sealed record TaskWireResponse
{
    public const string StatusOk = "ok";
    public const string StatusError = "error";

    [JsonPropertyName("mcpTask")]
    public required string McpTask { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("output")]
    public JsonElement? Output { get; init; }

    [JsonPropertyName("error")]
    public required string Error { get; init; }
}
