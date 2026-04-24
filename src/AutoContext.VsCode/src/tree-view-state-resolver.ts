import { ContextKeys } from './context-keys.js';
import { TreeViewNodeState } from './tree-view-node-state.js';
import type { CatalogEntry } from './types/catalog-entry.js';
import type { WorkspaceContextDetector } from './workspace-context-detector.js';
import type { AutoContextConfig } from './types/autocontext-config.js';
import { InstructionsCatalogEntry } from './instructions-catalog-entry.js';
import type { McpToolsCatalogEntry } from './mcp-tools-catalog-entry.js';
import { isToolEnabled } from './config-context-projector.js';

export class TreeViewStateResolver {
    constructor(private readonly detector: WorkspaceContextDetector) {}

    resolve(
        entry: CatalogEntry,
        config: AutoContextConfig,
        overrides?: ReadonlySet<string>,
    ): TreeViewNodeState {
        const flags = ContextKeys.forEntry(entry);
        if (flags.length > 0 && !flags.some(k => this.detector.get(k))) {
            return TreeViewNodeState.NotDetected;
        }

        const isEnabled = entry instanceof InstructionsCatalogEntry
            ? config.instructions?.[entry.fileName]?.enabled !== false
            : isToolEnabled(config, (entry as McpToolsCatalogEntry).toolName, (entry as McpToolsCatalogEntry).taskName);

        if (!isEnabled) {
            return TreeViewNodeState.Disabled;
        }

        if (overrides?.has(entry.contextKey)) {
            return TreeViewNodeState.Overridden;
        }

        return TreeViewNodeState.Enabled;
    }
}
