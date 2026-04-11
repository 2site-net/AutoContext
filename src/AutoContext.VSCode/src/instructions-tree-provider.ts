import * as vscode from 'vscode';
import type { InstructionsCatalog } from './instructions-catalog.js';
import { TreeViewNodeState } from './tree-view-node-state.js';
import { instructionsCategoryOrder, viewIds, contextKeys, treeViewLabels } from './ui-constants.js';
import { instructionScheme } from './instructions-content-provider.js';
import type { WorkspaceContextDetector } from './workspace-context-detector.js';
import type { InstructionsCatalogEntry } from './instructions-catalog-entry.js';
import type { TreeViewStateResolver } from './tree-view-state-resolver.js';
import type { TreeViewTooltip } from './tree-view-tooltip.js';
import type { InstructionsTreeCategoryNode } from './types/instructions-tree-category-node.js';
import type { InstructionsTreeNode } from './types/instructions-tree-node.js';

type TreeElement = InstructionsTreeCategoryNode | InstructionsTreeNode;

export class InstructionsTreeProvider implements vscode.TreeDataProvider<TreeElement>, vscode.Disposable {

    private readonly _onDidChangeTreeData = new vscode.EventEmitter<TreeElement | undefined>();
    readonly onDidChangeTreeData = this._onDidChangeTreeData.event;

    private _exportMode = false;
    private _showNotDetected = true;
    private readonly _checkedEntries = new Set<string>();
    private readonly treeView: vscode.TreeView<TreeElement>;
    private readonly disposables: vscode.Disposable[] = [];

    constructor(
        private readonly detector: WorkspaceContextDetector,
        private readonly catalog: InstructionsCatalog,
        private readonly stateResolver: TreeViewStateResolver,
        private readonly tooltip: TreeViewTooltip,
    ) {
        this.treeView = vscode.window.createTreeView(viewIds.Instructions, {
            treeDataProvider: this,
        });

        this.updateDescription();

        this.disposables.push(
            this.treeView,
            this._onDidChangeTreeData,
            detector.onDidDetect(() => this.refresh()),
            vscode.workspace.onDidChangeConfiguration(e => {
                if (e.affectsConfiguration('autocontext.instructions')) {
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
        const states = this.catalog.all.map(e => this.stateResolver.resolve(e, config, overrides));
        this.treeView.description = this.tooltip.description(states.filter(s => s.isActive()).length, this.catalog.count);
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
                state: this.stateResolver.resolve(entry, config, overrides),
            }))
            .filter(n => this._showNotDetected || n.state !== TreeViewNodeState.NotDetected)
            .sort((a, b) => a.state.sortOrder - b.state.sortOrder);
    }

    private categoryItem(node: InstructionsTreeCategoryNode): vscode.TreeItem {
        const item = new vscode.TreeItem(node.name, vscode.TreeItemCollapsibleState.Expanded);
        item.contextValue = 'category';
        const active = node.children.filter(n => n.state.isActive()).length;
        const total = this.catalog.all.filter(e => e.category === node.name).length;
        item.tooltip = this.tooltip.container(node.name, active, total);
        return item;
    }

    private instructionItem(node: InstructionsTreeNode): vscode.TreeItem {
        const item = new vscode.TreeItem(node.entry.label, vscode.TreeItemCollapsibleState.None);
        item.contextValue = `instruction.${node.state.value}`;

        switch (node.state) {
            case TreeViewNodeState.Enabled:
                item.iconPath = new vscode.ThemeIcon('check', new vscode.ThemeColor('testing.iconPassed'));
                break;
            case TreeViewNodeState.NotDetected:
                item.iconPath = new vscode.ThemeIcon('circle-slash', new vscode.ThemeColor('disabledForeground'));
                item.description = treeViewLabels.notDetected;
                break;
            case TreeViewNodeState.Disabled:
                item.iconPath = new vscode.ThemeIcon('x', new vscode.ThemeColor('testing.iconFailed'));
                item.description = treeViewLabels.disabled;
                break;
            case TreeViewNodeState.Overridden:
                item.iconPath = new vscode.ThemeIcon('file-symlink-file', new vscode.ThemeColor('terminal.ansiYellow'));
                item.description = treeViewLabels.overridden;
                break;
            default:
                node.state.throwIfUnknown();
        }

        if (this._exportMode && (node.state === TreeViewNodeState.Enabled || node.state === TreeViewNodeState.Disabled)) {
            item.checkboxState = this._checkedEntries.has(node.entry.settingId)
                ? vscode.TreeItemCheckboxState.Checked
                : vscode.TreeItemCheckboxState.Unchecked;
        }

        item.tooltip = this.tooltip.leaf(node.entry.label, node.state, node.entry.settingId);

        if (node.state === TreeViewNodeState.Overridden) {
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
