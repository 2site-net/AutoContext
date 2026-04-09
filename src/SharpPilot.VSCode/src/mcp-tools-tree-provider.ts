import * as vscode from 'vscode';
import type { McpToolsCatalog } from './mcp-tools-catalog.js';
import { ContextKeys } from './context-keys.js';
import { mcpToolGroupOrder, mcpToolCategoryOrder, viewIds, McpToolState, treeViewLabels } from './ui-constants.js';
import type { WorkspaceContextDetector } from './workspace-context-detector.js';
import type { McpToolsCatalogEntry } from './mcp-tools-catalog-entry.js';
import type { TreeViewGroupNode } from './types/tree-view-group-node.js';
import type { McpToolsTreeCategoryNode } from './types/mcp-tools-tree-category-node.js';
import type { McpToolsTreeNode } from './types/mcp-tools-tree-node.js';
import type { McpToolsTreeFeatureNode } from './types/mcp-tools-tree-feature-node.js';

type TreeElement = TreeViewGroupNode | McpToolsTreeCategoryNode | McpToolsTreeNode | McpToolsTreeFeatureNode;

const stateRank: Record<McpToolState, number> = {
    [McpToolState.Enabled]: 0,
    [McpToolState.Disabled]: 1,
    [McpToolState.NotDetected]: 2,
};

export class McpToolsTreeProvider implements vscode.TreeDataProvider<TreeElement>, vscode.Disposable {

    private readonly _onDidChangeTreeData = new vscode.EventEmitter<TreeElement | undefined>();
    readonly onDidChangeTreeData = this._onDidChangeTreeData.event;
    private _showNotDetected = true;
    private readonly treeView: vscode.TreeView<TreeElement>;
    private readonly disposables: vscode.Disposable[] = [];

    constructor(private readonly detector: WorkspaceContextDetector, private readonly catalog: McpToolsCatalog) {
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
        const enabled = this.catalog.all.filter(e =>
            McpToolsTreeProvider.resolveState(e, config, this.detector) === McpToolState.Enabled,
        ).length;
        this.treeView.description = `${enabled}/${this.catalog.count}`;
    }

    getTreeItem(element: TreeElement): vscode.TreeItem {
        switch (element.kind) {
            case 'group': return this.groupItem(element);
            case 'category': return this.categoryItem(element);
            case 'mcpTool': return this.mcpToolItem(element);
            case 'mcpToolFeature': return McpToolsTreeProvider.featureItem(element);
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
                        ? McpToolsTreeProvider.resolveState(leafEntry, config, this.detector)
                        : undefined,
                };
            })
            .filter(n => this._showNotDetected || (
                n.features.length === 0
                    ? n.leafState !== McpToolState.NotDetected
                    : n.features.some(f => f.state !== McpToolState.NotDetected)));
    }

    private resolveFeatures(toolName: string, config: vscode.WorkspaceConfiguration): McpToolsTreeFeatureNode[] {
        return this.catalog.all
            .filter(e => e.toolName === toolName && e.featureName !== undefined)
            .map(entry => ({
                kind: 'mcpToolFeature' as const,
                entry,
                state: McpToolsTreeProvider.resolveState(entry, config, this.detector),
            }));
    }

    private getVisibleFeatures(node: McpToolsTreeNode): McpToolsTreeFeatureNode[] {
        return [...node.features]
            .filter(n => this._showNotDetected || n.state !== McpToolState.NotDetected)
            .sort((a, b) => stateRank[a.state] - stateRank[b.state]);
    }

    private static resolveState(
        entry: McpToolsCatalogEntry,
        config: vscode.WorkspaceConfiguration,
        detector: WorkspaceContextDetector,
    ): McpToolState {
        const ctxKeys = ContextKeys.forEntry(entry);
        if (ctxKeys.length > 0 && !ctxKeys.some(k => detector.get(k))) {
            return McpToolState.NotDetected;
        }

        if (!config.get<boolean>(entry.settingId, true)) {
            return McpToolState.Disabled;
        }

        return McpToolState.Enabled;
    }

    private static countEnabled(tools: readonly McpToolsTreeNode[]): number {
        let enabled = 0;
        for (const t of tools) {
            if (t.features.length > 0) {
                enabled += t.features.filter(f => f.state === McpToolState.Enabled).length;
            } else if (t.leafState === McpToolState.Enabled) {
                enabled += 1;
            }
        }
        return enabled;
    }

    private static checkboxForParent(features: readonly McpToolsTreeFeatureNode[]): vscode.TreeItemCheckboxState | undefined {
        const detected = features.filter(t => t.state !== McpToolState.NotDetected);
        if (detected.length === 0) { return undefined; }

        return detected.every(t => t.state === McpToolState.Enabled)
            ? vscode.TreeItemCheckboxState.Checked
            : vscode.TreeItemCheckboxState.Unchecked;
    }

    private groupItem(node: TreeViewGroupNode): vscode.TreeItem {
        const item = new vscode.TreeItem(node.name, vscode.TreeItemCollapsibleState.Expanded);
        item.contextValue = 'group';
        const enabled = McpToolsTreeProvider.countEnabled(node.children.flatMap(c => c.children));
        item.tooltip = `${node.name}\n${enabled}/${node.totalEntries} ${treeViewLabels.featuresEnabledTooltip}`;
        return item;
    }

    private categoryItem(node: McpToolsTreeCategoryNode): vscode.TreeItem {
        const item = new vscode.TreeItem(node.name, vscode.TreeItemCollapsibleState.Expanded);
        item.contextValue = 'category';
        const enabled = McpToolsTreeProvider.countEnabled(node.children);
        item.tooltip = `${node.name}\n${enabled}/${node.totalEntries} ${treeViewLabels.featuresEnabledTooltip}`;
        return item;
    }

    private mcpToolItem(node: McpToolsTreeNode): vscode.TreeItem {
        if (node.features.length === 0) {
            return this.leafMcpToolItem(node);
        }

        const item = new vscode.TreeItem(node.toolName, vscode.TreeItemCollapsibleState.Expanded);
        item.contextValue = 'mcpTool';
        item.checkboxState = McpToolsTreeProvider.checkboxForParent(node.features);
        item.tooltip = McpToolsTreeProvider.parentTooltip(node.toolName, node.features);
        return item;
    }

    private leafMcpToolItem(node: McpToolsTreeNode): vscode.TreeItem {
        const entry = this.catalog.all.find(e => e.toolName === node.toolName && !e.featureName)!;
        const state = node.leafState!;

        const item = new vscode.TreeItem(node.toolName, vscode.TreeItemCollapsibleState.None);
        item.contextValue = 'mcpTool';

        if (state === McpToolState.NotDetected) {
            item.iconPath = new vscode.ThemeIcon('circle-slash', new vscode.ThemeColor('disabledForeground'));
            item.description = treeViewLabels.notDetected;
        } else {
            item.checkboxState = state === McpToolState.Enabled
                ? vscode.TreeItemCheckboxState.Checked
                : vscode.TreeItemCheckboxState.Unchecked;
        }

        item.tooltip = McpToolsTreeProvider.featureTooltip(entry, state);
        return item;
    }

    private static featureItem(node: McpToolsTreeFeatureNode): vscode.TreeItem {
        const item = new vscode.TreeItem(node.entry.label, vscode.TreeItemCollapsibleState.None);

        if (node.state === McpToolState.NotDetected) {
            item.iconPath = new vscode.ThemeIcon('circle-slash', new vscode.ThemeColor('disabledForeground'));
            item.description = treeViewLabels.notDetected;
        } else {
            item.checkboxState = node.state === McpToolState.Enabled
                ? vscode.TreeItemCheckboxState.Checked
                : vscode.TreeItemCheckboxState.Unchecked;
        }

        item.tooltip = McpToolsTreeProvider.featureTooltip(node.entry, node.state);
        return item;
    }

    private static featureTooltip(entry: McpToolsCatalogEntry, state: McpToolState): string {
        const lines = [entry.label];

        switch (state) {
            case McpToolState.Enabled:
                lines.push(treeViewLabels.enabledTooltip);
                break;
            case McpToolState.Disabled:
                lines.push(treeViewLabels.disabledTooltip);
                break;
            case McpToolState.NotDetected:
                lines.push(treeViewLabels.notDetectedTooltip);
                break;
            default: {
                const _: never = state;
                void _;
                break;
            }
        }

        lines.push(`${treeViewLabels.settingPrefix} ${entry.settingId}`);
        return lines.join('\n');
    }

    private static parentTooltip(toolName: string, features: readonly McpToolsTreeFeatureNode[]): string {
        const enabled = features.filter(s => s.state === McpToolState.Enabled).length;
        return `${toolName}\n${enabled}/${features.length} ${treeViewLabels.featuresEnabledTooltip}`;
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
                    if (sub.state === McpToolState.NotDetected) { continue; }
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
