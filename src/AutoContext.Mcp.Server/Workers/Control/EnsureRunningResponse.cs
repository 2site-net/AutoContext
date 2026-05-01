namespace AutoContext.Mcp.Server.Workers.Control;

using System.Text.Json.Serialization;

/// <summary>
/// Wire shape of an <c>EnsureRunning</c> response. <see cref="Status"/>
/// is always present; <see cref="Error"/> carries the failure message
/// when, and only when, <see cref="Status"/> is <c>"failed"</c>.
/// </summary>
internal sealed record EnsureRunningResponse
{
    public const string StatusReady = "ready";
    public const string StatusFailed = "failed";

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}
