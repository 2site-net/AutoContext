namespace AutoContext.Engine.Protocol.Messages.Agent;

/// <summary>
/// JSON-RPC method-name constants for the <c>Agent.*</c> family — the
/// agent-loop signal surface. The first five are fire-and-forget
/// notifications a hook script sends to the engine as an agent turn
/// progresses; <see cref="EventsSubscribe"/> is the server-streaming
/// re-broadcast every other client subscribes to. The engine never
/// observes the agent loop itself — the hook is the only sensor, so
/// these methods turn that sensor's readings into engine-broadcast
/// signals per <c>design § RPC surface (Agent.*)</c>. Lost
/// notifications are tolerable: the engine makes no correctness
/// decisions from agent events, only UX enrichment.
/// </summary>
public static class AgentMethods
{
    /// <summary>
    /// Fire-and-forget notification a hook sends when a sub-agent
    /// starts, carrying the session identity and the sub-agent's task
    /// prompt. Takes <see cref="JsonAgentSubagentStartedParams"/>.
    /// </summary>
    public const string SubagentStarted = "Agent.SubagentStarted";

    /// <summary>
    /// Fire-and-forget notification a hook sends when a sub-agent
    /// stops. Takes <see cref="JsonAgentSubagentStoppedParams"/>.
    /// </summary>
    public const string SubagentStopped = "Agent.SubagentStopped";

    /// <summary>
    /// Fire-and-forget notification a hook sends when the agent host
    /// is about to drop conversation history, so session-scoped
    /// derived state can be marked for re-evaluation. Takes
    /// <see cref="JsonAgentCompactedParams"/>.
    /// </summary>
    public const string Compacted = "Agent.Compacted";

    /// <summary>
    /// Fire-and-forget notification a hook sends after a tool returns,
    /// naming the tool and its outcome. Takes
    /// <see cref="JsonAgentToolUsedParams"/>.
    /// </summary>
    public const string ToolUsed = "Agent.ToolUsed";

    /// <summary>
    /// Fire-and-forget notification a hook sends when the agent
    /// finishes a turn. Takes <see cref="JsonAgentTurnEndedParams"/>.
    /// </summary>
    public const string TurnEnded = "Agent.TurnEnded";

    /// <summary>
    /// Server-streaming RPC. Subscribers receive one
    /// <see cref="JsonAgentEvent"/> per envelope as the engine
    /// re-broadcasts each of the five notifications above — a pure
    /// live tail with no snapshot-on-subscribe seed.
    /// </summary>
    public const string EventsSubscribe = "Agent.Events.Subscribe";
}
