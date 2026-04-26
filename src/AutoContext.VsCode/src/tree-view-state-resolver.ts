import { TreeViewNodeState } from './tree-view-node-state.js';
import type { WorkspaceContextDetector } from './workspace-context-detector.js';
import type { AutoContextConfig } from './types/autocontext-config.js';
import { InstructionsFileEntry } from './instructions-file-entry.js';
import type { McpToolEntry } from './mcp-tool-entry.js';
import type { McpTaskEntry } from './mcp-task-entry.js';
import { isToolEnabled } from './config-context-projector.js';

export class TreeViewStateResolver {
    constructor(private readonly detector: WorkspaceContextDetector) {}

    resolve(
        entry: InstructionsFileEntry,
        config: AutoContextConfig,
        overrides?: ReadonlySet<string>,
    ): TreeViewNodeState {
        const flags = entry.activationFlags;
        if (flags.length > 0 && !flags.some(k => this.detector.get(k))) {
            return TreeViewNodeState.NotDetected;
        }

        const isEnabled = config.instructions?.[entry.name]?.enabled !== false;

        if (!isEnabled) {
            return TreeViewNodeState.Disabled;
        }

        if (overrides?.has(entry.runtimeInfo.contextKey)) {
            return TreeViewNodeState.Overridden;
        }

        return TreeViewNodeState.Enabled;
    }

    resolveTask(tool: McpToolEntry, task: McpTaskEntry, config: AutoContextConfig): TreeViewNodeState {
        const flags = tool.activationFlags;
        if (flags.length > 0 && !flags.some(k => this.detector.get(k))) {
            return TreeViewNodeState.NotDetected;
        }

        if (!isToolEnabled(config, tool.name, task.name)) {
            return TreeViewNodeState.Disabled;
        }

        return TreeViewNodeState.Enabled;
    }
}
