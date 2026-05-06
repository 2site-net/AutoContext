import { McpItemEntry } from './mcp-item-entry.js';
import { McpToolRuntimeInfo } from './mcp-tool-runtime-info.js';
import { TreeViewNodeState } from './tree-view-node-state.js';
import type { McpToolEntry } from './mcp-tool-entry.js';
import type { McpRuntimeContext } from '#types/runtime-context.js';

/**
 * A task declared under a tool in `resources/mcp-tools.json`. Each task
 * has its own context key (`autocontext.mcpTools.<tool>.<task>`) and
 * holds a back-reference to its parent tool.
 */
export class McpTaskEntry extends McpItemEntry {
    readonly #runtimeInfo: McpToolRuntimeInfo;
    readonly #runtimeContext: McpRuntimeContext;

    constructor(
        name: string,
        description: string | undefined,
        readonly tool: McpToolEntry,
        runtimeContext: McpRuntimeContext,
    ) {
        super(name, description);
        this.#runtimeInfo = new McpToolRuntimeInfo(`${tool.name}.${name}`);
        this.#runtimeContext = runtimeContext;
    }

    get runtimeInfo(): McpToolRuntimeInfo {
        return this.#runtimeInfo;
    }

    /**
     * Resolves the current activation/configuration state of this task:
     * `NotDetected` if the parent tool's activation flags are non-empty
     * and none are detected, `Disabled` if the task is listed in
     * `disabledTasks`, otherwise `Enabled`. The task is independent of
     * the parent tool's enabled flag.
     */
    resolveState(): TreeViewNodeState {
        const flags = this.tool.activationFlags;
        if (flags.length > 0 && !flags.some(k => this.#runtimeContext.detector.get(k))) {
            return TreeViewNodeState.NotDetected;
        }

        const config = this.#runtimeContext.configManager.readSync();
        if (!config.isToolEnabled(this.tool.name, this.name)) {
            return TreeViewNodeState.Disabled;
        }

        return TreeViewNodeState.Enabled;
    }
}
