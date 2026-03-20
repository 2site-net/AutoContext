import * as vscode from 'vscode';
import type { ToggleEntry } from './config';

interface ToggleItem extends vscode.QuickPickItem {
    settingId: string;
}

const selectAllButton: vscode.QuickInputButton = { iconPath: new vscode.ThemeIcon('check-all'), tooltip: 'Select All' };
const clearAllButton: vscode.QuickInputButton = { iconPath: new vscode.ThemeIcon('clear-all'), tooltip: 'Clear All' };

export class MenuToggler {
    constructor(
        private readonly title: string,
        private readonly placeholder: string,
        private readonly entries: readonly ToggleEntry[],
    ) {}

    async toggle(): Promise<void> {
        const config = vscode.workspace.getConfiguration();

        const allEntries: ToggleItem[] = this.entries.map(entry => ({
            label: entry.label,
            description: entry.category,
            picked: config.get<boolean>(entry.settingId, true),
            settingId: entry.settingId,
        }));

        const selected = await new Promise<readonly ToggleItem[] | undefined>(resolve => {
            const qp = vscode.window.createQuickPick<ToggleItem>();
            qp.title = this.title;
            qp.placeholder = this.placeholder;
            qp.canSelectMany = true;
            qp.items = allEntries;
            qp.selectedItems = allEntries.filter(e => e.picked);
            qp.buttons = [selectAllButton, clearAllButton];

            qp.onDidTriggerButton(button => {
                qp.selectedItems = button === selectAllButton ? [...qp.items] : [];
            });

            let resolved = false;
            const done = (value: readonly ToggleItem[] | undefined) => {
                if (resolved) return;
                resolved = true;
                resolve(value);
                qp.dispose();
            };

            qp.onDidAccept(() => done(qp.selectedItems));
            qp.onDidHide(() => done(undefined));
            qp.show();
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
