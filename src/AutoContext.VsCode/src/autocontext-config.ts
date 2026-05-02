import type { AutoContextConfigInit } from '#types/autocontext-config-init.js';
import type { InstructionsFileConfigEntry } from '#types/instructions-file-config-entry.js';
import type { McpToolConfigEntry } from '#types/mcp-tool-config-entry.js';
import type { McpToolsDisabledSnapshot } from '#types/mcp-tools-disabled-snapshot.js';

/**
 * Runtime model of `.autocontext.json`. The class wraps the
 * deserialised JSON object: public mutable fields keep the same
 * shape that on-disk JSON exposes (so the manager can keep using
 * `config.mcpTools ??= {}` etc.) while the methods centralise the
 * read-side logic that used to live as free functions in
 * `autocontext-config-projector.ts`.
 *
 * `AutoContextConfigManager` owns the only writable instance held by
 * the extension; everything else receives it read-only by convention
 * and calls {@link isToolEnabled} / {@link getToolsDisabledSnapshot}.
 */
export class AutoContextConfig {
    version?: string;
    diagnostic?: {
        warnOnMissingId?: boolean;
    };
    instructions?: Record<string, InstructionsFileConfigEntry>;
    mcpTools?: Record<string, McpToolConfigEntry | false>;

    constructor(init?: AutoContextConfigInit) {
        if (init) {
            if (init.version !== undefined) this.version = init.version;
            if (init.diagnostic !== undefined) this.diagnostic = init.diagnostic;
            if (init.instructions !== undefined) this.instructions = init.instructions;
            if (init.mcpTools !== undefined) this.mcpTools = init.mcpTools;
        }
    }

    /**
     * Returns `false` if the entry for `toolName` is disabled (either
     * shorthand `false` or `{ enabled: false }`); when `taskName` is
     * supplied, returns `false` if the task is listed in
     * `disabledTasks`. Tasks are independent of their parent tool's
     * enabled state, so a task on a disabled parent can still be
     * "enabled" in its own right.
     */
    isToolEnabled(toolName: string, taskName?: string): boolean {
        const entry = this.mcpTools?.[toolName];
        if (entry === undefined) return true;

        if (taskName) {
            // Tasks are independent of the parent's enabled state.
            // `entry === false` (shorthand) has no disabledTasks → task is enabled.
            if (entry === false) return true;
            return !entry.disabledTasks?.includes(taskName);
        }

        if (entry === false) return false;
        if (entry.enabled === false) return false;
        return true;
    }

    /**
     * Projects the config into the tool/task disabled-state slice
     * consumed by `AutoContext.Mcp.Server` (broadcast over the
     * `extension-config` named pipe). Pure — no IO, no logging.
     */
    getToolsDisabledSnapshot(): McpToolsDisabledSnapshot {
        const disabledTools: string[] = [];
        const disabledTasks: Record<string, string[]> = {};

        const tools = this.mcpTools;
        if (tools) {
            for (const [name, entry] of Object.entries(tools)) {
                if (entry === false) {
                    disabledTools.push(name);
                    continue;
                }
                if (entry.enabled === false) {
                    disabledTools.push(name);
                }
                if (entry.disabledTasks && entry.disabledTasks.length > 0) {
                    disabledTasks[name] = [...entry.disabledTasks];
                }
            }
        }

        return { disabledTools, disabledTasks };
    }
}
