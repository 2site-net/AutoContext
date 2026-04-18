export interface AutoContextConfig {
    version?: string;
    diagnostic?: {
        warnOnMissingId?: boolean;
    };
    instructions?: Record<string, InstructionFileConfig>;
    mcpTools?: Record<string, McpToolConfig | false>;
}

export interface InstructionFileConfig {
    enabled?: false;
    version?: string;
    disabledInstructions?: string[];
}

export interface McpToolConfig {
    enabled?: false;
    version?: string;
    disabledFeatures?: string[];
}
