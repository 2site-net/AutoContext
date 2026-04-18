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
import { SemVer } from './semver.js';
import type { AutoContextConfigManager } from './autocontext-config.js';
import type { AutoContextConfig } from './types/autocontext-config.js';

type TreeElement = InstructionsTreeCategoryNode | InstructionsTreeNode;

export class InstructionsTreeProvider implements vscode.TreeDataProvider<TreeElement>, vscode.Disposable {

    private readonly _onDidChangeTreeData = new vscode.EventEmitter<TreeElement | undefined>();
    readonly onDidChangeTreeData = this._onDidChangeTreeData.event;

    private _exportMode = false;
    private _showNotDetected = true;
    private readonly _checkedEntries = new Set<string>();
    private readonly treeView: vscode.TreeView<TreeElement>;
    private readonly disposables: vscode.Disposable[] = [];
    private _config: AutoContextConfig;

    constructor(
        private readonly detector: WorkspaceContextDetector,
        private readonly catalog: InstructionsCatalog,
        private readonly stateResolver: TreeViewStateResolver,
        private readonly tooltip: TreeViewTooltip,
        private readonly configManager: AutoContextConfigManager,
    ) {
        this._config = configManager.readSync();

        this.treeView = vscode.window.createTreeView(viewIds.Instructions, {
            treeDataProvider: this,
        });

        this.updateDescription();

        this.disposables.push(
            this.treeView,
            this._onDidChangeTreeData,
            detector.onDidDetect(() => this.refresh()),
            configManager.onDidChange(() => {
                this._config = configManager.readSync();
                void configManager.read().then(c => {
                    this._config = c;
                    this.refresh();
                });
            }),
            this.treeView.onDidChangeCheckboxState(e => {
                for (const [item, state] of e.items) {
                    if (item.kind === 'instructions') {
                        if (state === vscode.TreeItemCheckboxState.Checked) {
                            this._checkedEntries.add(item.entry.contextKey);
                        } else {
                            this._checkedEntries.delete(item.entry.contextKey);
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
        const overrides = this.detector.getOverriddenContextKeys();
        const states = this.catalog.all.map(e => this.stateResolver.resolve(e, this._config, overrides));
        this.treeView.description = this.tooltip.description(states.filter(s => s.isActive()).length, this.catalog.count);
    }

    getTreeItem(element: TreeElement): vscode.TreeItem {
        if (element.kind === 'categoryNode') {
            return this.categoryItem(element);
        }

        return this.instructionItem(element);
    }

    getChildren(element?: TreeElement): TreeElement[] {
        if (element === undefined) {
            return this.getRootCategories();
        }

        if (element.kind === 'categoryNode') {
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
        const overrides = this.detector.getOverriddenContextKeys();
        const presentCategories = new Set(this.catalog.all.map(e => e.category));

        return instructionsCategoryOrder
            .filter(c => presentCategories.has(c))
            .map(name => ({
                kind: 'categoryNode' as const,
                name,
                children: this.resolveInstructions(name, overrides),
            }))
            .filter(c => c.children.length > 0);
    }

    private resolveInstructions(
        category: string,
        overrides: ReadonlySet<string>,
    ): InstructionsTreeNode[] {
        return this.catalog.all
            .filter(e => e.category === category)
            .map(entry => {
                const state = this.stateResolver.resolve(entry, this._config, overrides);
                const overrideVersion = state === TreeViewNodeState.Overridden
                    ? this.detector.getOverrideVersion(entry.fileName)
                    : undefined;
                const isOutdated = overrideVersion !== undefined
                    && entry.version !== undefined
                    && SemVer.isGreaterThan(entry.version, overrideVersion);
                return { kind: 'instructions' as const, entry, state, overrideVersion, isOutdated };
            })
            .filter(n => this._showNotDetected || n.state !== TreeViewNodeState.NotDetected)
            .sort((a, b) => a.state.sortOrder - b.state.sortOrder);
    }

    private categoryItem(node: InstructionsTreeCategoryNode): vscode.TreeItem {
        const item = new vscode.TreeItem(node.name, vscode.TreeItemCollapsibleState.Expanded);
        item.contextValue = 'categoryNode';
        const active = node.children.filter(n => n.state.isActive()).length;
        const total = this.catalog.all.filter(e => e.category === node.name).length;
        item.tooltip = this.tooltip.container(node.name, active, total);
        return item;
    }

    private instructionItem(node: InstructionsTreeNode): vscode.TreeItem {
        const item = new vscode.TreeItem(node.entry.label, vscode.TreeItemCollapsibleState.None);

        item.contextValue = `instruction.${node.state.value}${node.isOutdated ? '.outdated' : ''}${node.entry.hasChangelog ? '.hasChangelog' : ''}`;

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
                item.description = node.isOutdated ? treeViewLabels.outdated : treeViewLabels.overridden;
                break;
            default:
                node.state.throwIfUnknown();
        }

        if (this._exportMode && (node.state === TreeViewNodeState.Enabled || node.state === TreeViewNodeState.Disabled)) {
            item.checkboxState = this._checkedEntries.has(node.entry.contextKey)
                ? vscode.TreeItemCheckboxState.Checked
                : vscode.TreeItemCheckboxState.Unchecked;
        }

        item.tooltip = this.tooltip.leaf(
            node.entry.label, node.state, node.entry.contextKey, node.entry.description, node.entry.version,
            node.isOutdated ? treeViewLabels.outdatedTooltip : undefined,
        );

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
        return this.catalog.all.filter(e => this._checkedEntries.has(e.contextKey));
    }

    async enableInstruction(node: InstructionsTreeNode): Promise<void> {
        await this.configManager.setInstructionEnabled(node.entry.fileName, true);
    }

    async disableInstruction(node: InstructionsTreeNode): Promise<void> {
        await this.configManager.setInstructionEnabled(node.entry.fileName, false);
    }

    static async deleteOverride(node: InstructionsTreeNode): Promise<void> {
        const workspaceFolder = vscode.workspace.workspaceFolders?.[0];
        if (!workspaceFolder) { return; }

        if (node.isOutdated) {
            const choice = await vscode.window.showWarningMessage(
                `The local file (v${node.overrideVersion}) is behind AutoContext's version (v${node.entry.version}). Deleting will restore the latest version.`,
                { modal: true },
                'Delete',
            );
            if (choice !== 'Delete') { return; }
        }

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

    setBadge(value: number, tooltip: string): void {
        this.treeView.badge = value > 0 ? { value, tooltip } : undefined;
    }

    dismissBadgeOnNextReveal(onDismiss?: () => void | Promise<void>): void {
        const listener = this.treeView.onDidChangeVisibility(e => {
            if (e.visible) {
                this.setBadge(0, '');
                listener.dispose();
                void onDismiss?.();
            }
        });
        this.disposables.push(listener);
    }

    dispose(): void {
        for (const d of this.disposables) {
            d.dispose();
        }
    }
}
