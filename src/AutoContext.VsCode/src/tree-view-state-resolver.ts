import { ContextKeys } from './context-keys.js';
import { TreeViewNodeState } from './tree-view-node-state.js';
import type { CatalogEntry } from './types/catalog-entry.js';
import type { WorkspaceContextDetector } from './workspace-context-detector.js';
import type { AutoContextConfig } from './types/autocontext-config.js';
import { InstructionsCatalogEntry } from './instructions-catalog-entry.js';
import type { McpToolsCatalogEntry } from './mcp-tools-catalog-entry.js';
import { ConfigContextProjector } from './config-context-projector.js';

export class TreeViewStateResolver {
    constructor(private readonly detector: WorkspaceContextDetector) {}

    resolve(
        entry: CatalogEntry,
        config: AutoContextConfig,
        overrides?: ReadonlySet<string>,
    ): TreeViewNodeState {
        const ctxKeys = ContextKeys.forEntry(entry);
        if (ctxKeys.length > 0 && !ctxKeys.some(k => this.detector.get(k))) {
            return TreeViewNodeState.NotDetected;
        }

        const isEnabled = entry instanceof InstructionsCatalogEntry
            ? config.instructions?.[entry.fileName]?.enabled !== false
            : ConfigContextProjector.isToolEnabled(config, (entry as McpToolsCatalogEntry).toolName, (entry as McpToolsCatalogEntry).featureName);

        if (!isEnabled) {
            return TreeViewNodeState.Disabled;
        }

        if (overrides?.has(entry.settingId)) {
            return TreeViewNodeState.Overridden;
        }

        return TreeViewNodeState.Enabled;
    }
}
