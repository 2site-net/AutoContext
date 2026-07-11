namespace AutoContext.Engine.Protocol.Messages.Agent;

/// <summary>
/// Kebab-case wire-string constants for every <see cref="JsonAgentEvent.Kind"/>
/// value the engine re-broadcasts on <c>Agent.Events.Subscribe</c>.
/// Centralised here so both engine and clients reference the same
/// literals without copy-paste drift.
/// </summary>
public static class AgentEventKinds
{
    /// <summary>A sub-agent started; carries session and task prompt.</summary>
    public const string SubagentStarted = "subagent-started";

    /// <summary>A sub-agent stopped; carries session.</summary>
    public const string SubagentStopped = "subagent-stopped";

    /// <summary>The agent host compacted conversation history; carries session.</summary>
    public const string Compacted = "compacted";

    /// <summary>A tool returned; carries tool name and outcome.</summary>
    public const string ToolUsed = "tool-used";

    /// <summary>The agent finished a turn; carries session.</summary>
    public const string TurnEnded = "turn-ended";

    /// <summary>
    /// Terminal frame the engine writes when a subscriber's bounded
    /// buffer overflows. After this frame the engine completes the
    /// connection; the rest of the subscriber population keeps
    /// receiving events uninterrupted.
    /// </summary>
    public const string Dropped = "dropped";
}
