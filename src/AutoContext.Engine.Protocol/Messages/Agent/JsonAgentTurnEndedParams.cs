namespace AutoContext.Engine.Protocol.Messages.Agent;

using System.Text.Json.Serialization;

/// <summary>
/// Request params for <see cref="AgentMethods.TurnEnded"/>: the session
/// whose turn ended.
/// </summary>
public sealed record JsonAgentTurnEndedParams
{
    /// <summary>The agent-loop session whose turn ended.</summary>
    [JsonPropertyName("sessionId")]
    public string? SessionId { get; init; }
}
