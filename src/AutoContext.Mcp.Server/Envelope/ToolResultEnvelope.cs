namespace AutoContext.Mcp.Server.Envelope;

using System.Text.Json.Serialization;

/// <summary>
/// The uniform tool-result envelope returned by every MCP Tool. Outer
/// shape is identical across all tools; per-tool variation lives only
/// inside <see cref="ToolResultEntry.Output"/>.
/// </summary>
public sealed record ToolResultEnvelope
{
    public const string StatusOk = "ok";
    public const string StatusError = "error";
    public const string StatusPartial = "partial";

    [JsonPropertyName("tool")]
    public required string Tool { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("summary")]
    public required ToolResultSummary Summary { get; init; }

    [JsonPropertyName("result")]
    public required IReadOnlyList<ToolResultEntry> Result { get; init; }

    [JsonPropertyName("errors")]
    public required IReadOnlyList<ToolResultError> Errors { get; init; }
}
