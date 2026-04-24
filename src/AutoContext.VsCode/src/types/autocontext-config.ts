export interface AutoContextConfig {
    version?: string;
    diagnostic?: {
        warnOnMissingId?: boolean;
    };
    instructions?: Record<string, InstructionsFileConfig>;
    mcpTools?: Record<string, McpToolConfig | false>;
}

export interface InstructionsFileConfig {
    enabled?: false;
    version?: string;
    disabledInstructions?: string[];
}

export interface McpToolConfig {
    enabled?: false;
    version?: string;
    disabledTasks?: string[];
}
