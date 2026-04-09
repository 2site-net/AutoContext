import * as vscode from 'vscode';
import { ContextKeys } from './context-keys.js';
import { TreeViewNodeState } from './tree-view-node-state.js';
import type { CatalogEntry } from './types/catalog-entry.js';
import type { WorkspaceContextDetector } from './workspace-context-detector.js';

export class TreeViewStateResolver {
    constructor(private readonly detector: WorkspaceContextDetector) {}

    resolve(
        entry: CatalogEntry,
        config: vscode.WorkspaceConfiguration,
        overrides?: ReadonlySet<string>,
    ): TreeViewNodeState {
        const ctxKeys = ContextKeys.forEntry(entry);
        if (ctxKeys.length > 0 && !ctxKeys.some(k => this.detector.get(k))) {
            return TreeViewNodeState.NotDetected;
        }

        if (!config.get<boolean>(entry.settingId, true)) {
            return TreeViewNodeState.Disabled;
        }

        if (overrides?.has(entry.settingId)) {
            return TreeViewNodeState.Overridden;
        }

        return TreeViewNodeState.Enabled;
    }
}
