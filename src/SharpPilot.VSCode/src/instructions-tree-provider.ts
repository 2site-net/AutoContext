import * as vscode from 'vscode';
import type { InstructionsCatalog } from './instructions-catalog.js';
import { ContextKeys } from './context-keys.js';
import { instructionsCategoryOrder, viewIds, contextKeys } from './ui-constants.js';
import { instructionScheme } from './instructions-content-provider.js';
import type { WorkspaceContextDetector } from './workspace-context-detector.js';
import type { InstructionsCatalogEntry } from './instructions-catalog-entry.js';

export const InstructionState = {
    Active: 'active',
    NotDetected: 'notDetected',
    Disabled: 'disabled',
    Overridden: 'overridden',
} as const;

export type InstructionState = typeof InstructionState[keyof typeof InstructionState];

type TreeElement = CategoryNode | InstructionNode;

interface CategoryNode {
    readonly kind: 'category';
    readonly name: string;
}

interface InstructionNode {
    readonly kind: 'instruction';
    readonly entry: InstructionsCatalogEntry;
    readonly state: InstructionState;
}



// Sort rank: Active & Overridden first, then Disabled, then NotDetected.
const stateRank: Record<InstructionState, number> = {
    [InstructionState.Active]: 0,
    [InstructionState.Overridden]: 1,
    [InstructionState.Disabled]: 2,
    [InstructionState.NotDetected]: 3,
};

export class InstructionsTreeProvider implements vscode.TreeDataProvider<TreeElement>, vscode.Disposable {

    private readonly _onDidChangeTreeData = new vscode.EventEmitter<TreeElement | undefined>();
    readonly onDidChangeTreeData = this._onDidChangeTreeData.event;

    private _exportMode = false;
    private _showNotDetected = true;
    private readonly _checkedEntries = new Set<string>();
    private readonly treeView: vscode.TreeView<TreeElement>;
    private readonly disposables: vscode.Disposable[] = [];

    constructor(private readonly detector: WorkspaceContextDetector, private readonly catalog: InstructionsCatalog) {
        this.treeView = vscode.window.createTreeView(viewIds.Instructions, {
            treeDataProvider: this,
        });

        this.updateDescription();

        this.disposables.push(
            this.treeView,
            this._onDidChangeTreeData,
            detector.onDidDetect(() => this.refresh()),
            vscode.workspace.onDidChangeConfiguration(e => {
                if (e.affectsConfiguration('sharppilot.instructions')) {
                    this.refresh();
                }
            }),
            this.treeView.onDidChangeCheckboxState(e => {
                for (const [item, state] of e.items) {
                    if (item.kind === 'instruction') {
                        if (state === vscode.TreeItemCheckboxState.Checked) {
                            this._checkedEntries.add(item.entry.settingId);
                        } else {
                            this._checkedEntries.delete(item.entry.settingId);
                        }
                    }
                }
            }),
        );
    }

    refresh(): void {
        this.updateDescription();
        this._onDidChangeTreeData.fire(undefined);
    }

    private updateDescription(): void {
        const config = vscode.workspace.getConfiguration();
        const overrides = this.detector.getOverriddenSettingIds();
        const active = this.catalog.all.filter(e => {
            const state = InstructionsTreeProvider.resolveState(e, config, this.detector, overrides);
            return state === InstructionState.Active || state === InstructionState.Overridden;
        }).length;
        this.treeView.description = `${active}/${this.catalog.count}`;
    }

    getTreeItem(element: TreeElement): vscode.TreeItem {
        if (element.kind === 'category') {
            return this.categoryItem(element);
        }

        return this.instructionItem(element);
    }

    getChildren(element?: TreeElement): TreeElement[] {
        if (element === undefined) {
            return this.getRootCategories();
        }

        if (element.kind === 'category') {
            return this.getInstructionsForCategory(element.name);
        }

        return [];
    }

    get showNotDetected(): boolean {
        return this._showNotDetected;
    }

    set showNotDetected(value: boolean) {
        if (this._showNotDetected === value) { return; }
        this._showNotDetected = value;
        this.refresh();
    }

    private getRootCategories(): CategoryNode[] {
        const children = (name: string) => this.getInstructionsForCategory(name);
        const presentCategories = new Set(this.catalog.all.map(e => e.category));
        return instructionsCategoryOrder
            .filter(c => presentCategories.has(c) && children(c).length > 0)
            .map(name => ({ kind: 'category' as const, name }));
    }

    private getInstructionsForCategory(category: string): InstructionNode[] {
        const config = vscode.workspace.getConfiguration();
        const overrides = this.detector.getOverriddenSettingIds();

        return this.catalog.all
            .filter(e => e.category === category)
            .map(entry => ({
                kind: 'instruction' as const,
                entry,
                state: InstructionsTreeProvider.resolveState(entry, config, this.detector, overrides),
            }))
            .filter(n => this._showNotDetected || n.state !== InstructionState.NotDetected)
            .sort((a, b) => stateRank[a.state] - stateRank[b.state]);
    }

    private static resolveState(
        entry: InstructionsCatalogEntry,
        config: vscode.WorkspaceConfiguration,
        detector: WorkspaceContextDetector,
        overrides: ReadonlySet<string>,
    ): InstructionState {
        const ctxKeys = ContextKeys.forEntry(entry);
        if (ctxKeys.length > 0 && !ctxKeys.some(k => detector.get(k))) {
            return InstructionState.NotDetected;
        }

        if (!config.get<boolean>(entry.settingId, true)) {
            return InstructionState.Disabled;
        }

        if (overrides.has(entry.settingId)) {
            return InstructionState.Overridden;
        }

        return InstructionState.Active;
    }

    private categoryItem(node: CategoryNode): vscode.TreeItem {
        const item = new vscode.TreeItem(node.name, vscode.TreeItemCollapsibleState.Expanded);
        item.contextValue = 'category';
        const config = vscode.workspace.getConfiguration();
        const overrides = this.detector.getOverriddenSettingIds();
        const entries = this.catalog.all.filter(e => e.category === node.name);
        const active = entries.filter(e => {
            const state = InstructionsTreeProvider.resolveState(e, config, this.detector, overrides);
            return state === InstructionState.Active || state === InstructionState.Overridden;
        }).length;
        item.tooltip = `${node.name}\n${active}/${entries.length} active`;
        return item;
    }

    private instructionItem(node: InstructionNode): vscode.TreeItem {
        const item = new vscode.TreeItem(node.entry.label, vscode.TreeItemCollapsibleState.None);
        item.contextValue = `instruction.${node.state}`;

        switch (node.state) {
            case InstructionState.Active:
                item.iconPath = new vscode.ThemeIcon('check', new vscode.ThemeColor('testing.iconPassed'));
                break;
            case InstructionState.NotDetected:
                item.iconPath = new vscode.ThemeIcon('circle-slash', new vscode.ThemeColor('disabledForeground'));
                item.description = 'not detected';
                break;
            case InstructionState.Disabled:
                item.iconPath = new vscode.ThemeIcon('x', new vscode.ThemeColor('testing.iconFailed'));
                item.description = 'disabled';
                break;
            case InstructionState.Overridden:
                item.iconPath = new vscode.ThemeIcon('file-symlink-file', new vscode.ThemeColor('terminal.ansiYellow'));
                item.description = 'overridden';
                break;
            default: {
                const _: never = node.state;
                void _;
                break;
            }
        }

        if (this._exportMode && (node.state === InstructionState.Active || node.state === InstructionState.Disabled)) {
            item.checkboxState = this._checkedEntries.has(node.entry.settingId)
                ? vscode.TreeItemCheckboxState.Checked
                : vscode.TreeItemCheckboxState.Unchecked;
        }

        item.tooltip = InstructionsTreeProvider.tooltip(node);

        if (node.state === InstructionState.Overridden) {
            const workspaceFolder = vscode.workspace.workspaceFolders?.[0];
            if (workspaceFolder) {
                item.command = {
                    command: 'vscode.open',
                    title: 'Open Override',
                    arguments: [vscode.Uri.joinPath(workspaceFolder.uri, node.entry.targetPath)],
                };
            }
        } else {
            item.command = {
                command: 'vscode.open',
                title: 'Open Instruction',
                arguments: [vscode.Uri.from({ scheme: instructionScheme, path: node.entry.fileName })],
            };
        }

        return item;
    }

    private static tooltip(node: InstructionNode): string {
        const lines = [node.entry.label];

        switch (node.state) {
            case InstructionState.Active:
                lines.push('Active — included in Copilot context');
                break;
            case InstructionState.NotDetected:
                lines.push('Not detected — workspace lacks matching files');
                break;
            case InstructionState.Disabled:
                lines.push('Disabled — turned off in settings');
                break;
            case InstructionState.Overridden:
                lines.push('Overridden — local .github/instructions file found');
                break;
            default: {
                const _: never = node.state;
                void _;
                break;
            }
        }

        lines.push(`Setting: ${node.entry.settingId}`);
        return lines.join('\n');
    }

    enterExportMode(): void {
        this._exportMode = true;
        this._checkedEntries.clear();
        void vscode.commands.executeCommand('setContext', contextKeys.ExportMode, true);
        this.refresh();
    }

    cancelExportMode(): void {
        this._exportMode = false;
        this._checkedEntries.clear();
        void vscode.commands.executeCommand('setContext', contextKeys.ExportMode, false);
        this.refresh();
    }

    getCheckedEntries(): readonly InstructionsCatalogEntry[] {
        return this.catalog.all.filter(e => this._checkedEntries.has(e.settingId));
    }

    static async enableInstruction(node: InstructionNode): Promise<void> {
        await vscode.workspace.getConfiguration().update(node.entry.settingId, true, vscode.ConfigurationTarget.Global);
    }

    static async disableInstruction(node: InstructionNode): Promise<void> {
        await vscode.workspace.getConfiguration().update(node.entry.settingId, false, vscode.ConfigurationTarget.Global);
    }

    static async deleteOverride(node: InstructionNode): Promise<void> {
        const workspaceFolder = vscode.workspace.workspaceFolders?.[0];
        if (!workspaceFolder) { return; }

        const targetUri = vscode.Uri.joinPath(workspaceFolder.uri, node.entry.targetPath);

        for (const tab of vscode.window.tabGroups.all.flatMap(g => g.tabs)) {
            const tabUri = (tab.input as { uri?: vscode.Uri })?.uri;
            if (tabUri?.toString() === targetUri.toString()) {
                await vscode.window.tabGroups.close(tab);
            }
        }

        await vscode.workspace.fs.delete(targetUri);
    }

    static async showOriginal(node: InstructionNode): Promise<void> {
        const uri = vscode.Uri.from({ scheme: instructionScheme, path: node.entry.fileName });
        await vscode.commands.executeCommand('vscode.open', uri);
    }

    dispose(): void {
        for (const d of this.disposables) {
            d.dispose();
        }
    }
}
