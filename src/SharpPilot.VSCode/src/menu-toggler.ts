import * as vscode from 'vscode';
import type { CatalogEntry } from './catalog-entry.js';

interface ToggleItem extends vscode.QuickPickItem {
    settingId: string;
    isCategory?: boolean;
    category?: string;
}

const selectAllButton: vscode.QuickInputButton = { iconPath: new vscode.ThemeIcon('check-all'), tooltip: 'Select All' };
const clearAllButton: vscode.QuickInputButton = { iconPath: new vscode.ThemeIcon('clear-all'), tooltip: 'Clear All' };

export class MenuToggler {
    constructor(
        private readonly title: string,
        private readonly placeholder: string,
        private readonly entries: readonly CatalogEntry[],
        private readonly getOverrides?: () => ReadonlySet<string>,
    ) {}

    async toggle(): Promise<void> {
        const config = vscode.workspace.getConfiguration();
        const overrides = this.getOverrides?.() ?? new Set<string>();

        const allItems = MenuToggler.buildItems(this.entries, config, overrides);
        const settings = MenuToggler.settingItems(allItems);
        const categories = MenuToggler.categoryItems(allItems);

        const initiallySelected = [
            ...settings.filter(e => e.picked),
            ...categories.filter(cat => {
                const members = MenuToggler.itemsInCategory(allItems, cat.category);
                return members.length > 0 && members.every(m => m.picked);
            }),
        ];

        const selected = await new Promise<readonly ToggleItem[] | undefined>(resolve => {
            const qp = vscode.window.createQuickPick<ToggleItem>();
            qp.title = this.title;
            qp.placeholder = this.placeholder;
            qp.canSelectMany = true;
            qp.items = allItems;
            qp.selectedItems = initiallySelected;
            qp.buttons = [selectAllButton, clearAllButton];

            let suppressCascade = false;
            const selectedSet = new Set(initiallySelected.map(i => i.settingId));

            qp.onDidChangeSelection(selection => {
                if (suppressCascade) return;

                const newSet = new Set(selection.map(i => i.settingId));

                for (const cat of categories) {
                    const wasSelected = selectedSet.has(cat.settingId);
                    const nowSelected = newSet.has(cat.settingId);

                    if (wasSelected !== nowSelected) {
                        const members = MenuToggler.itemsInCategory(allItems, cat.category);
                        if (nowSelected) {
                            for (const m of members) newSet.add(m.settingId);
                        } else {
                            for (const m of members) newSet.delete(m.settingId);
                        }
                    }
                }

                for (const cat of categories) {
                    const members = MenuToggler.itemsInCategory(allItems, cat.category);
                    const allSelected = members.length > 0 && members.every(m => newSet.has(m.settingId));
                    if (allSelected) {
                        newSet.add(cat.settingId);
                    } else {
                        newSet.delete(cat.settingId);
                    }
                }

                selectedSet.clear();
                for (const id of newSet) selectedSet.add(id);

                const newSelection = allItems.filter(i => newSet.has(i.settingId));
                if (newSelection.length !== selection.length || newSelection.some((item, idx) => item !== selection[idx])) {
                    suppressCascade = true;
                    qp.selectedItems = newSelection;
                    suppressCascade = false;
                }
            });

            qp.onDidTriggerButton(button => {
                const selectableItems = allItems.filter(i => i.kind !== vscode.QuickPickItemKind.Separator);
                suppressCascade = true;
                qp.selectedItems = button === selectAllButton ? selectableItems : [];
                suppressCascade = false;

                selectedSet.clear();
                if (button === selectAllButton) {
                    for (const i of selectableItems) selectedSet.add(i.settingId);
                }
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

        for (const entry of settings) {
            const shouldBeEnabled = selectedIds.has(entry.settingId);
            const isEnabled = config.get<boolean>(entry.settingId, true);

            if (shouldBeEnabled !== isEnabled) {
                await config.update(entry.settingId, shouldBeEnabled, vscode.ConfigurationTarget.Global);
            }
        }
    }

    private static buildItems(entries: readonly CatalogEntry[], config: vscode.WorkspaceConfiguration, overrides: ReadonlySet<string>): ToggleItem[] {
        const items: ToggleItem[] = [];
        let lastCategory: string | undefined;

        for (const entry of entries) {
            if (entry.category !== lastCategory) {
                lastCategory = entry.category;
                items.push({
                    label: entry.category,
                    kind: vscode.QuickPickItemKind.Separator,
                    settingId: '',
                });
                items.push({
                    label: `$(folder) ${entry.category}`,
                    description: 'toggle all',
                    settingId: `__category__${entry.category}`,
                    isCategory: true,
                    category: entry.category,
                });
            }

            items.push({
                label: entry.label,
                description: overrides.has(entry.settingId) ? `${entry.category} $(file-symlink-directory)` : entry.category,
                picked: config.get<boolean>(entry.settingId, true),
                settingId: entry.settingId,
                category: entry.category,
            });
        }

        return items;
    }

    private static settingItems(items: readonly ToggleItem[]): ToggleItem[] {
        return items.filter(i => !i.isCategory && i.kind !== vscode.QuickPickItemKind.Separator);
    }

    private static categoryItems(items: readonly ToggleItem[]): (ToggleItem & { category: string })[] {
        return items.filter((i): i is ToggleItem & { category: string } => !!i.isCategory);
    }

    private static itemsInCategory(items: readonly ToggleItem[], category: string): ToggleItem[] {
        return items.filter(i => i.category === category && !i.isCategory && i.kind !== vscode.QuickPickItemKind.Separator);
    }
}
