export interface VersionedDisabledIds {
    version: string;
    ids: string[];
}

export interface AutoContextConfig {
    version?: string;
    diagnostic?: {
        warnOnMissingId?: boolean;
    };
    instructions?: {
        disabled?: Record<string, string[] | VersionedDisabledIds>;
    };
    mcpTools?: {
        disabled?: string[];
    };
}
