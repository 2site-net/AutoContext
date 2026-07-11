namespace AutoContext.Engine.Protocol.Messages.Agent;

using System.Text.Json.Serialization;

/// <summary>
/// Payload of an <c>Agent.Events.Subscribe</c> stream frame: one
/// agent-loop transition identified by its <see cref="Kind"/>. The
/// engine maps each inbound <c>Agent.*</c> notification onto this
/// unified envelope and fans it out to every subscriber — one event per
/// envelope, a pure live tail with no snapshot-on-subscribe seed.
/// </summary>
/// <remarks>
/// The kebab-case <see cref="Kind"/> literals are defined as constants
/// on <see cref="AgentEventKinds"/>. Field presence follows the kind:
/// <list type="bullet">
/// <item><see cref="SessionId"/> is populated on every kind except the
/// terminal <c>dropped</c> frame.</item>
/// <item><see cref="TaskPrompt"/> is populated only on
/// <c>subagent-started</c>.</item>
/// <item><see cref="ToolName"/> and <see cref="Outcome"/> are populated
/// only on <c>tool-used</c>.</item>
/// <item><see cref="Reason"/> is populated only on the terminal
/// <c>dropped</c> frame the engine writes when a slow subscriber fills
/// its bounded buffer.</item>
/// </list>
/// Absent fields are omitted from the wire JSON by the
/// <see cref="Serialization.ProtocolJsonContext"/>'s default
/// <see cref="JsonIgnoreCondition.WhenWritingNull"/> policy.
/// </remarks>
public sealed record JsonAgentEvent
{
    /// <summary>
    /// Kebab-case wire string identifying the transition. One of the
    /// constants on <see cref="AgentEventKinds"/>.
    /// </summary>
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = string.Empty;

    /// <summary>The tool-call outcome, on <c>tool-used</c> only.</summary>
    [JsonPropertyName("outcome")]
    public string? Outcome { get; init; }

    /// <summary>The drop reason, on the terminal <c>dropped</c> frame only.</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    /// <summary>The agent-loop session the event belongs to, when applicable.</summary>
    [JsonPropertyName("sessionId")]
    public string? SessionId { get; init; }

    /// <summary>The sub-agent task prompt, on <c>subagent-started</c> only.</summary>
    [JsonPropertyName("taskPrompt")]
    public string? TaskPrompt { get; init; }

    /// <summary>The tool that returned, on <c>tool-used</c> only.</summary>
    [JsonPropertyName("toolName")]
    public string? ToolName { get; init; }
}
