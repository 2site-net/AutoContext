import * as vscode from 'vscode';
import { tools } from './config';

export class ToolsToggler {
    async toggle(): Promise<void> {
        const config = vscode.workspace.getConfiguration();

        const allEntries = tools.map(entry => ({
            label: entry.label,
            description: entry.category,
            picked: config.get<boolean>(entry.settingId, true),
            settingId: entry.settingId,
        }));

        const selected = await vscode.window.showQuickPick(allEntries, {
            canPickMany: true,
            title: 'SharpPilot: Toggle Tools',
            placeHolder: 'Select tools to enable',
        });

        if (!selected) {
            return;
        }

        const selectedIds = new Set(selected.map(s => s.settingId));

        for (const entry of allEntries) {
            const shouldBeEnabled = selectedIds.has(entry.settingId);
            const isEnabled = config.get<boolean>(entry.settingId, true);

            if (shouldBeEnabled !== isEnabled) {
                await config.update(entry.settingId, shouldBeEnabled, vscode.ConfigurationTarget.Global);
            }
        }
    }
}
