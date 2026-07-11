namespace AutoContext.Engine.Protocol.Messages.Agent;

using System.Text.Json.Serialization;

/// <summary>
/// Request params for <see cref="AgentMethods.ToolUsed"/>: the session
/// the turn belongs to, the tool that returned, and the outcome the hook
/// observed.
/// </summary>
public sealed record JsonAgentToolUsedParams
{
    /// <summary>The outcome the hook observed for the tool call.</summary>
    [JsonPropertyName("outcome")]
    public string? Outcome { get; init; }

    /// <summary>The agent-loop session the tool call belongs to.</summary>
    [JsonPropertyName("sessionId")]
    public string? SessionId { get; init; }

    /// <summary>The name of the tool that returned.</summary>
    [JsonPropertyName("toolName")]
    public string? ToolName { get; init; }
}
