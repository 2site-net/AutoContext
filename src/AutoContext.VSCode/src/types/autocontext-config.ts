import type { VersionedDisabledIds } from './versioned-disabled-ids.js';

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
