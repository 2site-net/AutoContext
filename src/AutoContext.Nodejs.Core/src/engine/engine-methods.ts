/** Wire method names of the engine's RPC surface. */
export const EngineMethods = {
    Hello: 'Engine.Hello',
    Shutdown: 'Engine.Shutdown',
    RegistryEntries: 'Engine.RegistryEntries',
    LifecycleNotification: 'Engine.Lifecycle',

    ConfigGet: 'Config.Get',
    ConfigToggleFile: 'Config.ToggleFile',
    ConfigToggleRule: 'Config.ToggleRule',
    ConfigSubscribe: 'Config.Subscribe',

    InstructionsList: 'Instructions.List',
    InstructionsCategories: 'Instructions.Categories',
    InstructionsGet: 'Instructions.Get',
    InstructionsGetAll: 'Instructions.GetAll',
    InstructionsGetAlwaysAttached: 'Instructions.GetAlwaysAttached',
    InstructionsGetRaw: 'Instructions.GetRaw',
    InstructionsSearchContent: 'Instructions.SearchContent',
    InstructionsSearchByMetadata: 'Instructions.SearchByMetadata',
    InstructionsSubscribe: 'Instructions.Subscribe',

    WorkspaceDetect: 'Workspace.Detect',
    WorkspaceInfo: 'Workspace.Info',

    McpToolsList: 'McpTools.List',
    McpToolsInvoke: 'McpTools.Invoke',

    DiscoveryRouteForPrompt: 'Discovery.RouteForPrompt',
    DiscoveryRouteForTool: 'Discovery.RouteForTool',

    AgentSubagentStarted: 'Agent.SubagentStarted',
    AgentSubagentStopped: 'Agent.SubagentStopped',
    AgentCompacted: 'Agent.Compacted',
    AgentToolUsed: 'Agent.ToolUsed',
    AgentTurnEnded: 'Agent.TurnEnded',
    AgentEventsSubscribe: 'Agent.Events.Subscribe',

    LogsGetEngine: 'Logs.GetEngine',
    LogsTailEngine: 'Logs.TailEngine',
    LogsGetWorker: 'Logs.GetWorker',
    LogsTailWorker: 'Logs.TailWorker',
} as const;
