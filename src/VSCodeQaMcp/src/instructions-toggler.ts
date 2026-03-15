import * as vscode from 'vscode';
import { instructions, tools } from './config';

export class InstructionsToggler {
    async toggle(): Promise<void> {
        const config = vscode.workspace.getConfiguration();

        const allEntries = [
            ...instructions.map(entry => ({
                label: entry.label,
                description: entry.category,
                picked: config.get<boolean>(entry.settingId, true),
                settingId: entry.settingId,
            })),
            ...tools.map(entry => ({
                label: entry.label,
                description: entry.category,
                picked: config.get<boolean>(entry.settingId, true),
                settingId: entry.settingId,
            })),
        ];

        const selected = await vscode.window.showQuickPick(allEntries, {
            canPickMany: true,
            title: 'QA-MCP: Toggle Instructions & Tools',
            placeHolder: 'Select instructions and tools to enable',
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
