export interface AutoContextConfig {
    version?: string;
    diagnostic?: {
        warnOnMissingId?: boolean;
    };
    instructions?: {
        disabled?: Record<string, string[]>;
    };
    mcpTools?: {
        disabled?: string[];
    };
}
