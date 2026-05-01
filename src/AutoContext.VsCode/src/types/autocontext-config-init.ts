import type { InstructionsFileConfigEntry } from './instructions-file-config-entry.js';
import type { McpToolConfigEntry } from './mcp-tool-config-entry.js';

/** Initialiser shape accepted by `new AutoContextConfig(...)`. */
export interface AutoContextConfigInit {
    version?: string;
    diagnostic?: {
        warnOnMissingId?: boolean;
    };
    instructions?: Record<string, InstructionsFileConfigEntry>;
    mcpTools?: Record<string, McpToolConfigEntry | false>;
}
