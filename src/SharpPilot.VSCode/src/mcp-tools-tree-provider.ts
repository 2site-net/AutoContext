import * as vscode from 'vscode';
import type { McpToolsCatalog } from './mcp-tools-catalog.js';
import { mcpToolGroupOrder, mcpToolCategoryOrder, viewIds, TreeViewNodeState, treeViewStateSortOrder, treeViewLabels } from './ui-constants.js';
import type { WorkspaceContextDetector } from './workspace-context-detector.js';
import type { TreeViewStateResolver } from './tree-view-state-resolver.js';
import type { TreeViewTooltip } from './tree-view-tooltip.js';
import type { TreeViewGroupNode } from './types/tree-view-group-node.js';
import type { McpToolsTreeCategoryNode } from './types/mcp-tools-tree-category-node.js';
import type { McpToolsTreeNode } from './types/mcp-tools-tree-node.js';
import type { McpToolsTreeFeatureNode } from './types/mcp-tools-tree-feature-node.js';

type TreeElement = TreeViewGroupNode | McpToolsTreeCategoryNode | McpToolsTreeNode | McpToolsTreeFeatureNode;

export class McpToolsTreeProvider implements vscode.TreeDataProvider<TreeElement>, vscode.Disposable {

    private readonly _onDidChangeTreeData = new vscode.EventEmitter<TreeElement | undefined>();
    readonly onDidChangeTreeData = this._onDidChangeTreeData.event;
    private _showNotDetected = true;
    private readonly treeView: vscode.TreeView<TreeElement>;
    private readonly disposables: vscode.Disposable[] = [];

    constructor(
        detector: WorkspaceContextDetector,
        private readonly catalog: McpToolsCatalog,
        private readonly stateResolver: TreeViewStateResolver,
        private readonly tooltip: TreeViewTooltip,
    ) {
        this.treeView = vscode.window.createTreeView(viewIds.Tools, {
            treeDataProvider: this,
            manageCheckboxStateManually: true,
        });

        this.updateDescription();

        this.disposables.push(
            this.treeView,
            this._onDidChangeTreeData,
            detector.onDidDetect(() => this.refresh()),
            vscode.workspace.onDidChangeConfiguration(e => {
                if (e.affectsConfiguration('sharppilot.tools')) {
                    this.refresh();
                }
            }),
            this.treeView.onDidChangeCheckboxState(e => {
                void this.handleCheckboxChange(e.items);
            }),
        );
    }

    refresh(): void {
        this.updateDescription();
        this._onDidChangeTreeData.fire(undefined);
    }

    private updateDescription(): void {
        const config = vscode.workspace.getConfiguration();
        const states = this.catalog.all.map(e => this.stateResolver.resolve(e, config));
        this.treeView.description = this.tooltip.description(this.stateResolver.countActive(states), this.catalog.count);
    }

    getTreeItem(element: TreeElement): vscode.TreeItem {
        switch (element.kind) {
            case 'group': return this.groupItem(element);
            case 'category': return this.categoryItem(element);
            case 'mcpTool': return this.mcpToolItem(element);
            case 'mcpToolFeature': return this.featureItem(element);
        }
    }

    getChildren(element?: TreeElement): TreeElement[] {
        if (element === undefined) {
            return this.buildTree();
        }

        switch (element.kind) {
            case 'group': return [...element.children];
            case 'category': return [...element.children];
            case 'mcpTool': return this.getVisibleFeatures(element);
            default: return [];
        }
    }

    get showNotDetected(): boolean {
        return this._showNotDetected;
    }

    set showNotDetected(value: boolean) {
        if (this._showNotDetected === value) { return; }
        this._showNotDetected = value;
        this.refresh();
    }

    private buildTree(): TreeViewGroupNode[] {
        const config = vscode.workspace.getConfiguration();
        const presentGroups = new Set(this.catalog.all.map(e => e.group));

        return mcpToolGroupOrder
            .filter(g => presentGroups.has(g))
            .map(name => {
                const children = this.resolveCategories(name, config);
                return {
                    kind: 'group' as const,
                    name,
                    children,
                    totalEntries: this.catalog.all.filter(e => e.group === name).length,
                };
            })
            .filter(g => g.children.length > 0);
    }

    private resolveCategories(group: string, config: vscode.WorkspaceConfiguration): McpToolsTreeCategoryNode[] {
        const presentCategories = new Set(
            this.catalog.all.filter(e => e.group === group).map(e => e.category),
        );
        return mcpToolCategoryOrder
            .filter(c => presentCategories.has(c))
            .map(name => {
                const children = this.resolveTools(name, config);
                return {
                    kind: 'category' as const,
                    group,
                    name,
                    children,
                    totalEntries: this.catalog.all.filter(e => e.category === name).length,
                };
            })
            .filter(c => c.children.length > 0);
    }

    private resolveTools(category: string, config: vscode.WorkspaceConfiguration): McpToolsTreeNode[] {
        const toolNames = [...new Set(
            this.catalog.all
                .filter(e => e.category === category)
                .map(e => e.toolName),
        )];

        return toolNames
            .map(toolName => {
                const features = this.resolveFeatures(toolName, config);
                const leafEntry = features.length === 0
                    ? this.catalog.all.find(e => e.toolName === toolName && !e.featureName)
                    : undefined;
                return {
                    kind: 'mcpTool' as const,
                    toolName,
                    category,
                    features,
                    leafState: leafEntry
                        ? this.stateResolver.resolve(leafEntry, config)
                        : undefined,
                };
            })
            .filter(n => this._showNotDetected || (
                n.features.length === 0
                    ? n.leafState !== TreeViewNodeState.NotDetected
                    : n.features.some(f => f.state !== TreeViewNodeState.NotDetected)));
    }

    private resolveFeatures(toolName: string, config: vscode.WorkspaceConfiguration): McpToolsTreeFeatureNode[] {
        return this.catalog.all
            .filter(e => e.toolName === toolName && e.featureName !== undefined)
            .map(entry => ({
                kind: 'mcpToolFeature' as const,
                entry,
                state: this.stateResolver.resolve(entry, config),
            }));
    }

    private getVisibleFeatures(node: McpToolsTreeNode): McpToolsTreeFeatureNode[] {
        return [...node.features]
            .filter(n => this._showNotDetected || n.state !== TreeViewNodeState.NotDetected)
            .sort((a, b) => treeViewStateSortOrder[a.state] - treeViewStateSortOrder[b.state]);
    }

    private countActive(tools: readonly McpToolsTreeNode[]): number {
        const states: TreeViewNodeState[] = [];
        for (const t of tools) {
            if (t.features.length > 0) {
                states.push(...t.features.map(f => f.state));
            } else if (t.leafState) {
                states.push(t.leafState);
            }
        }
        return this.stateResolver.countActive(states);
    }

    private checkboxForParent(features: readonly McpToolsTreeFeatureNode[]): vscode.TreeItemCheckboxState | undefined {
        const detected = features.filter(t => t.state !== TreeViewNodeState.NotDetected);
        if (detected.length === 0) { return undefined; }

        return detected.every(t => this.stateResolver.isActive(t.state))
            ? vscode.TreeItemCheckboxState.Checked
            : vscode.TreeItemCheckboxState.Unchecked;
    }

    private groupItem(node: TreeViewGroupNode): vscode.TreeItem {
        const item = new vscode.TreeItem(node.name, vscode.TreeItemCollapsibleState.Expanded);
        item.contextValue = 'group';
        const active = this.countActive(node.children.flatMap(c => c.children));
        item.tooltip = this.tooltip.container(node.name, active, node.totalEntries);
        return item;
    }

    private categoryItem(node: McpToolsTreeCategoryNode): vscode.TreeItem {
        const item = new vscode.TreeItem(node.name, vscode.TreeItemCollapsibleState.Expanded);
        item.contextValue = 'category';
        const active = this.countActive(node.children);
        item.tooltip = this.tooltip.container(node.name, active, node.totalEntries);
        return item;
    }

    private mcpToolItem(node: McpToolsTreeNode): vscode.TreeItem {
        if (node.features.length === 0) {
            return this.leafMcpToolItem(node);
        }

        const item = new vscode.TreeItem(node.toolName, vscode.TreeItemCollapsibleState.Expanded);
        item.contextValue = 'mcpTool';
        item.checkboxState = this.checkboxForParent(node.features);
        const active = this.stateResolver.countActive(node.features.map(f => f.state));
        item.tooltip = this.tooltip.container(node.toolName, active, node.features.length);
        return item;
    }

    private leafMcpToolItem(node: McpToolsTreeNode): vscode.TreeItem {
        const entry = this.catalog.all.find(e => e.toolName === node.toolName && !e.featureName)!;
        const state = node.leafState!;

        const item = new vscode.TreeItem(node.toolName, vscode.TreeItemCollapsibleState.None);
        item.contextValue = 'mcpTool';

        if (state === TreeViewNodeState.NotDetected) {
            item.iconPath = new vscode.ThemeIcon('circle-slash', new vscode.ThemeColor('disabledForeground'));
            item.description = treeViewLabels.notDetected;
        } else {
            item.checkboxState = this.stateResolver.isActive(state)
                ? vscode.TreeItemCheckboxState.Checked
                : vscode.TreeItemCheckboxState.Unchecked;
        }

        item.tooltip = this.tooltip.leaf(entry.label, state, entry.settingId);
        return item;
    }

    private featureItem(node: McpToolsTreeFeatureNode): vscode.TreeItem {
        const item = new vscode.TreeItem(node.entry.label, vscode.TreeItemCollapsibleState.None);

        if (node.state === TreeViewNodeState.NotDetected) {
            item.iconPath = new vscode.ThemeIcon('circle-slash', new vscode.ThemeColor('disabledForeground'));
            item.description = treeViewLabels.notDetected;
        } else {
            item.checkboxState = this.stateResolver.isActive(node.state)
                ? vscode.TreeItemCheckboxState.Checked
                : vscode.TreeItemCheckboxState.Unchecked;
        }

        item.tooltip = this.tooltip.leaf(node.entry.label, node.state, node.entry.settingId);
        return item;
    }

    private async handleCheckboxChange(items: ReadonlyArray<readonly [TreeElement, vscode.TreeItemCheckboxState]>): Promise<void> {
        const config = vscode.workspace.getConfiguration();
        const enabled = (s: vscode.TreeItemCheckboxState) => s === vscode.TreeItemCheckboxState.Checked;

        const updates: Thenable<void>[] = [];

        for (const [element, state] of items) {
            if (element.kind === 'mcpToolFeature') {
                updates.push(config.update(element.entry.settingId, enabled(state), vscode.ConfigurationTarget.Global));
                continue;
            }

            if (element.kind !== 'mcpTool') { continue; }

            if (element.features.length === 0) {
                const entry = this.catalog.all.find(e => e.toolName === element.toolName && !e.featureName)!;
                updates.push(config.update(entry.settingId, enabled(state), vscode.ConfigurationTarget.Global));
            } else {
                for (const sub of element.features) {
                    if (sub.state === TreeViewNodeState.NotDetected) { continue; }
                    updates.push(config.update(sub.entry.settingId, enabled(state), vscode.ConfigurationTarget.Global));
                }
            }
        }

        await Promise.all(updates);
    }

    dispose(): void {
        for (const d of this.disposables) {
            d.dispose();
        }
    }
}
