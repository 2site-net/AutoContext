namespace AutoContext.Engine.Protocol.Messages.Agent;

using System.Text.Json.Serialization;

/// <summary>
/// Request params for <see cref="AgentMethods.SubagentStarted"/>: the
/// session the sub-agent belongs to and the task prompt it was started
/// with.
/// </summary>
public sealed record JsonAgentSubagentStartedParams
{
    /// <summary>The agent-loop session the sub-agent belongs to.</summary>
    [JsonPropertyName("sessionId")]
    public string? SessionId { get; init; }

    /// <summary>The task prompt the sub-agent was started with.</summary>
    [JsonPropertyName("taskPrompt")]
    public string? TaskPrompt { get; init; }
}
