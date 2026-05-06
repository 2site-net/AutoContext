import { McpItemEntry } from './mcp-item-entry.js';
import { McpToolRuntimeInfo } from './mcp-tool-runtime-info.js';
import { TreeViewNodeState } from './tree-view-node-state.js';
import type { McpToolEntry } from './mcp-tool-entry.js';
import type { WorkspaceContextDetector } from './workspace-context-detector.js';
import type { AutoContextConfigManager } from './autocontext-config-manager.js';

/**
 * A task declared under a tool in `resources/mcp-tools.json`. Each task
 * has its own context key (`autocontext.mcpTools.<tool>.<task>`) and
 * holds a back-reference to its parent tool.
 */
export class McpTaskEntry extends McpItemEntry {
    readonly #runtimeInfo: McpToolRuntimeInfo;
    readonly #detector: WorkspaceContextDetector;
    readonly #configManager: AutoContextConfigManager;

    constructor(
        name: string,
        description: string | undefined,
        readonly tool: McpToolEntry,
        detector: WorkspaceContextDetector,
        configManager: AutoContextConfigManager,
    ) {
        super(name, description);
        this.#runtimeInfo = new McpToolRuntimeInfo(`${tool.name}.${name}`);
        this.#detector = detector;
        this.#configManager = configManager;
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
        if (flags.length > 0 && !flags.some(k => this.#detector.get(k))) {
            return TreeViewNodeState.NotDetected;
        }

        const config = this.#configManager.readSync();
        if (!config.isToolEnabled(this.tool.name, this.name)) {
            return TreeViewNodeState.Disabled;
        }

        return TreeViewNodeState.Enabled;
    }
}
