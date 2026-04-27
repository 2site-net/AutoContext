namespace AutoContext.Mcp.Server.Workers.Protocol;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// One per-task pipe request. <c>WorkerClient</c> opens one connection
/// per task in a tool invocation and writes exactly this payload.
/// </summary>
public sealed record TaskRequest
{
    [JsonPropertyName("mcpTask")]
    public required string McpTask { get; init; }

    [JsonPropertyName("data")]
    public required JsonElement Data { get; init; }

    [JsonPropertyName("editorconfig")]
    public required IReadOnlyDictionary<string, string> EditorConfig { get; init; }

    /// <summary>
    /// Short opaque id minted once per <c>tools/call</c> invocation by
    /// <c>McpSdkAdapter</c>. Carried on every per-task <see cref="TaskRequest"/>
    /// in the same invocation, surfaced into the worker via
    /// <c>CorrelationScope</c>, and stamped onto every log record so a
    /// single tool call can be traced end-to-end across the
    /// extension/server/worker process boundary.
    /// </summary>
    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }
}
