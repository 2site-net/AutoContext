namespace AutoContext.Mcp.Server.Tools.Results;

using System.Text.Json.Serialization;

/// <summary>
/// Aggregate counters for a tool invocation.
/// </summary>
public sealed record ToolResultSummary
{
    [JsonPropertyName("taskCount")]
    public required int TaskCount { get; init; }

    [JsonPropertyName("successCount")]
    public required int SuccessCount { get; init; }

    [JsonPropertyName("failureCount")]
    public required int FailureCount { get; init; }

    [JsonPropertyName("elapsedMs")]
    public required int ElapsedMs { get; init; }
}
