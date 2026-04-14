import * as vscode from 'vscode';
import type { CatalogEntry } from './types/catalog-entry.js';
import type { InstructionsCatalog } from './instructions-catalog.js';
import type { McpToolsCatalog } from './mcp-tools-catalog.js';
import { ContextKeys } from './context-keys.js';
import type { WorkspaceContextDetector } from './workspace-context-detector.js';

export class AutoConfigurer {
    static async configure(detector: WorkspaceContextDetector, instructionsCatalog: InstructionsCatalog, toolsCatalog: McpToolsCatalog): Promise<void> {
        const config = vscode.workspace.getConfiguration();
        const allEntries: readonly CatalogEntry[] = [...instructionsCatalog.all, ...toolsCatalog.all];

        let enabled = 0;
        const updates: Thenable<void>[] = [];

        for (const entry of allEntries) {
            const keys = ContextKeys.forEntry(entry);
            const relevant = keys.length === 0 || keys.some(k => detector.get(k));

            if (config.get<boolean>(entry.settingId, true) !== relevant) {
                updates.push(config.update(entry.settingId, relevant, vscode.ConfigurationTarget.Global));
            }

            if (relevant) {
                enabled++;
            }
        }

        await Promise.all(updates);

        await vscode.window.showInformationMessage(
            `AutoContext: Enabled ${enabled} of ${allEntries.length} items for this workspace.`,
        );
    }
}
