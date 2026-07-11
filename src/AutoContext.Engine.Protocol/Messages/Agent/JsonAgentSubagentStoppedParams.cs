namespace AutoContext.Engine.Protocol.Messages.Agent;

using System.Text.Json.Serialization;

/// <summary>
/// Request params for <see cref="AgentMethods.SubagentStopped"/>: the
/// session whose sub-agent stopped.
/// </summary>
public sealed record JsonAgentSubagentStoppedParams
{
    /// <summary>The agent-loop session whose sub-agent stopped.</summary>
    [JsonPropertyName("sessionId")]
    public string? SessionId { get; init; }
}
