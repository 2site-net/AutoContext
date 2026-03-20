import * as vscode from 'vscode';
import { instructions, tools, contextKeysForEntry, type ToggleEntry } from './config';
import type { WorkspaceContextDetector } from './workspace-context-detector';

export async function autoConfigure(detector: WorkspaceContextDetector): Promise<void> {
    const config = vscode.workspace.getConfiguration();
    const allEntries: readonly ToggleEntry[] = [...instructions, ...tools];

    let enabled = 0;

    for (const entry of allEntries) {
        const keys = contextKeysForEntry(entry);
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
