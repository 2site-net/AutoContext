import * as vscode from 'vscode';
import type { InstructionsCatalog } from './instructions-catalog.js';
import { ContextKeys } from './context-keys.js';
import { instructionsCategoryOrder, viewIds, contextKeys, InstructionsState, treeViewLabels } from './ui-constants.js';
import { instructionScheme } from './instructions-content-provider.js';
import type { WorkspaceContextDetector } from './workspace-context-detector.js';
import type { InstructionsCatalogEntry } from './instructions-catalog-entry.js';
import type { InstructionsTreeCategoryNode } from './types/instructions-tree-category-node.js';
import type { InstructionsTreeNode } from './types/instructions-tree-node.js';

type TreeElement = InstructionsTreeCategoryNode | InstructionsTreeNode;

// Sort rank: Active & Overridden first, then Disabled, then NotDetected.
const stateRank: Record<InstructionsState, number> = {
    [InstructionsState.Active]: 0,
    [InstructionsState.Overridden]: 1,
    [InstructionsState.Disabled]: 2,
    [InstructionsState.NotDetected]: 3,
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
                    if (item.kind === 'instructions') {
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
            return state === InstructionsState.Active || state === InstructionsState.Overridden;
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
            return [...element.children];
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

    private getRootCategories(): InstructionsTreeCategoryNode[] {
        const config = vscode.workspace.getConfiguration();
        const overrides = this.detector.getOverriddenSettingIds();
        const presentCategories = new Set(this.catalog.all.map(e => e.category));

        return instructionsCategoryOrder
            .filter(c => presentCategories.has(c))
            .map(name => ({
                kind: 'category' as const,
                name,
                children: this.resolveInstructions(name, config, overrides),
            }))
            .filter(c => c.children.length > 0);
    }

    private resolveInstructions(
        category: string,
        config: vscode.WorkspaceConfiguration,
        overrides: ReadonlySet<string>,
    ): InstructionsTreeNode[] {
        return this.catalog.all
            .filter(e => e.category === category)
            .map(entry => ({
                kind: 'instructions' as const,
                entry,
                state: InstructionsTreeProvider.resolveState(entry, config, this.detector, overrides),
            }))
            .filter(n => this._showNotDetected || n.state !== InstructionsState.NotDetected)
            .sort((a, b) => stateRank[a.state] - stateRank[b.state]);
    }

    private static resolveState(
        entry: InstructionsCatalogEntry,
        config: vscode.WorkspaceConfiguration,
        detector: WorkspaceContextDetector,
        overrides: ReadonlySet<string>,
    ): InstructionsState {
        const ctxKeys = ContextKeys.forEntry(entry);
        if (ctxKeys.length > 0 && !ctxKeys.some(k => detector.get(k))) {
            return InstructionsState.NotDetected;
        }

        if (!config.get<boolean>(entry.settingId, true)) {
            return InstructionsState.Disabled;
        }

        if (overrides.has(entry.settingId)) {
            return InstructionsState.Overridden;
        }

        return InstructionsState.Active;
    }

    private categoryItem(node: InstructionsTreeCategoryNode): vscode.TreeItem {
        const item = new vscode.TreeItem(node.name, vscode.TreeItemCollapsibleState.Expanded);
        item.contextValue = 'category';
        const active = node.children.filter(n =>
            n.state === InstructionsState.Active || n.state === InstructionsState.Overridden,
        ).length;
        const total = this.catalog.all.filter(e => e.category === node.name).length;
        item.tooltip = `${node.name}\n${active}/${total} ${treeViewLabels.activeSuffix}`;
        return item;
    }

    private instructionItem(node: InstructionsTreeNode): vscode.TreeItem {
        const item = new vscode.TreeItem(node.entry.label, vscode.TreeItemCollapsibleState.None);
        item.contextValue = `instruction.${node.state}`;

        switch (node.state) {
            case InstructionsState.Active:
                item.iconPath = new vscode.ThemeIcon('check', new vscode.ThemeColor('testing.iconPassed'));
                break;
            case InstructionsState.NotDetected:
                item.iconPath = new vscode.ThemeIcon('circle-slash', new vscode.ThemeColor('disabledForeground'));
                item.description = treeViewLabels.notDetected;
                break;
            case InstructionsState.Disabled:
                item.iconPath = new vscode.ThemeIcon('x', new vscode.ThemeColor('testing.iconFailed'));
                item.description = treeViewLabels.disabled;
                break;
            case InstructionsState.Overridden:
                item.iconPath = new vscode.ThemeIcon('file-symlink-file', new vscode.ThemeColor('terminal.ansiYellow'));
                item.description = treeViewLabels.overridden;
                break;
            default: {
                const _: never = node.state;
                void _;
                break;
            }
        }

        if (this._exportMode && (node.state === InstructionsState.Active || node.state === InstructionsState.Disabled)) {
            item.checkboxState = this._checkedEntries.has(node.entry.settingId)
                ? vscode.TreeItemCheckboxState.Checked
                : vscode.TreeItemCheckboxState.Unchecked;
        }

        item.tooltip = InstructionsTreeProvider.tooltip(node);

        if (node.state === InstructionsState.Overridden) {
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

    private static tooltip(node: InstructionsTreeNode): string {
        const lines = [node.entry.label];

        switch (node.state) {
            case InstructionsState.Active:
                lines.push(treeViewLabels.activeTooltip);
                break;
            case InstructionsState.NotDetected:
                lines.push(treeViewLabels.notDetectedTooltip);
                break;
            case InstructionsState.Disabled:
                lines.push(treeViewLabels.disabledTooltip);
                break;
            case InstructionsState.Overridden:
                lines.push(treeViewLabels.overriddenTooltip);
                break;
            default: {
                const _: never = node.state;
                void _;
                break;
            }
        }

        lines.push(`${treeViewLabels.settingPrefix} ${node.entry.settingId}`);
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

    static async enableInstruction(node: InstructionsTreeNode): Promise<void> {
        await vscode.workspace.getConfiguration().update(node.entry.settingId, true, vscode.ConfigurationTarget.Global);
    }

    static async disableInstruction(node: InstructionsTreeNode): Promise<void> {
        await vscode.workspace.getConfiguration().update(node.entry.settingId, false, vscode.ConfigurationTarget.Global);
    }

    static async deleteOverride(node: InstructionsTreeNode): Promise<void> {
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

    static async showOriginal(node: InstructionsTreeNode): Promise<void> {
        const uri = vscode.Uri.from({ scheme: instructionScheme, path: node.entry.fileName });
        await vscode.commands.executeCommand('vscode.open', uri);
    }

    dispose(): void {
        for (const d of this.disposables) {
            d.dispose();
        }
    }
}
