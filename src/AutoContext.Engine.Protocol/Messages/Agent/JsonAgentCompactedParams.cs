namespace AutoContext.Engine.Protocol.Messages.Agent;

using System.Text.Json.Serialization;

/// <summary>
/// Request params for <see cref="AgentMethods.Compacted"/>: the session
/// whose conversation history the agent host compacted.
/// </summary>
public sealed record JsonAgentCompactedParams
{
    /// <summary>The agent-loop session that was compacted.</summary>
    [JsonPropertyName("sessionId")]
    public string? SessionId { get; init; }
}
