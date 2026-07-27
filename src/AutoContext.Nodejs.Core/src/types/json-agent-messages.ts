// Wire shapes of the Agent.* surface. The five notification params
// records carry no response, so a wrong property name silently
// no-ops — every payload the manager sends is typed against these.

/** Parameters of the Agent.SubagentStarted notification. */
export interface JsonAgentSubagentStartedParams {
    readonly sessionId?: string;
    readonly taskPrompt?: string;
}

/** Parameters of the Agent.SubagentStopped notification. */
export interface JsonAgentSubagentStoppedParams {
    readonly sessionId?: string;
}

/** Parameters of the Agent.Compacted notification. */
export interface JsonAgentCompactedParams {
    readonly sessionId?: string;
}

/** Parameters of the Agent.ToolUsed notification. */
export interface JsonAgentToolUsedParams {
    readonly sessionId?: string;
    readonly toolName?: string;
    readonly outcome?: string;
}

/** Parameters of the Agent.TurnEnded notification. */
export interface JsonAgentTurnEndedParams {
    readonly sessionId?: string;
}

/** Agent-loop event broadcast on Agent.Events.Subscribe. */
export interface JsonAgentEvent {
    readonly kind:
    | 'subagent-started'
    | 'subagent-stopped'
    | 'compacted'
    | 'tool-used'
    | 'turn-ended'
    | 'dropped';
    readonly sessionId?: string;
    readonly taskPrompt?: string;
    readonly toolName?: string;
    readonly outcome?: string;
    readonly reason?: string;
}
