namespace AutoContext.Mcp.Tools.Envelope;

using System.Text.Json.Serialization;

/// <summary>
/// One envelope-level error. Populated only when dispatch never happened
/// (manifest validation rejected input, all tasks failed before dispatch,
/// malformed wire response, etc.). Per-task failures live in
/// <see cref="ToolResultEntry.Error"/> instead.
/// </summary>
public sealed record ToolResultError
{
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }
}
