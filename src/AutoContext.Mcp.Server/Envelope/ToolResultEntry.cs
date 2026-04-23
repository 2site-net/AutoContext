namespace AutoContext.Mcp.Server.Envelope;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// One per-task entry inside the uniform tool-result envelope.
/// <see cref="Output"/> is null when <see cref="Status"/> is "error";
/// <see cref="Error"/> is empty when <see cref="Status"/> is "ok".
/// </summary>
public sealed record ToolResultEntry
{
    [JsonPropertyName("task")]
    public required string Task { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("elapsedMs")]
    public required int ElapsedMs { get; init; }

    [JsonPropertyName("output")]
    public JsonElement? Output { get; init; }

    [JsonPropertyName("error")]
    public required string Error { get; init; }
}
