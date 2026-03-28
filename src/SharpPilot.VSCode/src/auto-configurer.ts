import * as vscode from 'vscode';
import type { CatalogEntry } from './catalog-entry.js';
import { instructionsCatalog } from './instructions-catalog.js';
import { toolsCatalog } from './tools-catalog.js';
import { ContextKeys } from './context-keys.js';
import type { WorkspaceContextDetector } from './workspace-context-detector.js';

export async function autoConfigure(detector: WorkspaceContextDetector): Promise<void> {
    const config = vscode.workspace.getConfiguration();
    const allEntries: readonly CatalogEntry[] = [...instructionsCatalog.all, ...toolsCatalog.all];

    let enabled = 0;

    for (const entry of allEntries) {
        const keys = ContextKeys.forEntry(entry);
        const relevant = keys.length === 0 || keys.some(k => detector.get(k));

        if (config.get<boolean>(entry.settingId, true) !== relevant) {
            await config.update(entry.settingId, relevant, vscode.ConfigurationTarget.Global);
        }

        if (relevant) {
            enabled++;
        }
    }

    await vscode.window.showInformationMessage(
        `SharpPilot: Enabled ${enabled} of ${allEntries.length} items for this workspace.`,
    );
}
