import * as vscode from 'vscode';
import type { McpToolsCatalog } from './mcp-tools-catalog.js';
import { TreeViewNodeState } from './tree-view-node-state.js';
import { viewIds, treeViewLabels } from './ui-constants.js';
import type { WorkspaceContextDetector } from './workspace-context-detector.js';
import type { TreeViewStateResolver } from './tree-view-state-resolver.js';
import type { TreeViewTooltip } from './tree-view-tooltip.js';
import type { HealthMonitorServer } from './health-monitor.js';
import type { McpServerProvider } from './mcp-server-provider.js';
import type { TreeViewServerLabelNode } from './types/tree-view-server-label-node.js';
import type { McpToolsTreeCategoryNode } from './types/mcp-tools-tree-category-node.js';
import type { McpToolsTreeNode } from './types/mcp-tools-tree-node.js';
import type { McpTaskTreeNode } from './types/mcp-task-tree-node.js';
import type { AutoContextConfigManager } from './autocontext-config.js';
import type { AutoContextConfig } from './types/autocontext-config.js';

type TreeElement = TreeViewServerLabelNode | McpToolsTreeCategoryNode | McpToolsTreeNode | McpTaskTreeNode;

export class McpToolsTreeProvider implements vscode.TreeDataProvider<TreeElement>, vscode.Disposable {

    private readonly _onDidChangeTreeData = new vscode.EventEmitter<TreeElement | undefined>();
    readonly onDidChangeTreeData = this._onDidChangeTreeData.event;
    private _showNotDetected = true;
    private readonly treeView: vscode.TreeView<TreeElement>;
    private readonly disposables: vscode.Disposable[] = [];
    private _config: AutoContextConfig;

    constructor(
        detector: WorkspaceContextDetector,
        private readonly catalog: McpToolsCatalog,
        private readonly stateResolver: TreeViewStateResolver,
        private readonly tooltip: TreeViewTooltip,
        private readonly configManager: AutoContextConfigManager,
        private readonly outputChannel: vscode.OutputChannel,
        private readonly healthMonitor?: HealthMonitorServer,
        private readonly serverProvider?: McpServerProvider,
    ) {
        this._config = configManager.readSync();

        this.treeView = vscode.window.createTreeView(viewIds.Tools, {
            treeDataProvider: this,
            manageCheckboxStateManually: true,
        });

        this.updateDescription();

        this.disposables.push(
            this.treeView,
            this._onDidChangeTreeData,
            detector.onDidDetect(() => this.refresh()),
            configManager.onDidChange(() => {
                void configManager.read().then(c => {
                    this._config = c;
                    this.refresh();
                }).catch(err =>
                    this.outputChannel.appendLine(`[McpToolsTree] Failed to update config: ${err instanceof Error ? err.message : err}`),
                );
            }),
            this.treeView.onDidChangeCheckboxState(e => {
                void this.handleCheckboxChange(e.items).catch(err =>
                    this.outputChannel.appendLine(`[McpToolsTree] Failed to handle checkbox change: ${err instanceof Error ? err.message : err}`),
                );
            }),
        );

        if (healthMonitor) {
            this.disposables.push(
                healthMonitor.onDidChange(() => this.refresh()),
            );
        }
    }

    refresh(): void {
        this.updateDescription();
        this._onDidChangeTreeData.fire(undefined);
    }

    private updateDescription(): void {
        const states = this.catalog.all.map(e => this.stateResolver.resolve(e, this._config));
        this.treeView.description = this.tooltip.description(states.filter(s => s.isActive()).length, this.catalog.count);
    }

    getTreeItem(element: TreeElement): vscode.TreeItem {
        switch (element.kind) {
            case 'serverNode': return this.serverLabelItem(element);
            case 'categoryNode': return this.categoryItem(element);
            case 'mcpToolNode': return this.mcpToolItem(element);
            case 'mcpTaskNode': return this.taskItem(element);
        }
    }

    getChildren(element?: TreeElement): TreeElement[] {
        if (element === undefined) {
            return this.buildTree();
        }

        switch (element.kind) {
            case 'serverNode': return [...element.children];
            case 'categoryNode': return [...element.children];
            case 'mcpToolNode': return element.isLeaf ? [] : this.getVisibleTasks(element);
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

    private buildTree(): TreeViewServerLabelNode[] {
        const presentServerLabels = new Set(this.catalog.all.map(e => e.serverLabel));

        return this.catalog.serverLabelOrder
            .filter(g => presentServerLabels.has(g))
            .map(name => {
                const children = this.resolveCategories(name, this._config);
                return {
                    kind: 'serverNode' as const,
                    name,
                    children,
                    totalEntries: this.catalog.all.filter(e => e.serverLabel === name).length,
                };
            })
            .filter(g => g.children.length > 0);
    }

    private resolveCategories(serverLabel: string, config: AutoContextConfig): McpToolsTreeCategoryNode[] {
        const presentCategories = new Set(
            this.catalog.all.filter(e => e.serverLabel === serverLabel).map(e => e.category),
        );
        return this.catalog.categoryOrder
            .filter(c => presentCategories.has(c))
            .map(name => {
                const children = this.resolveTools(name, config);
                return {
                    kind: 'categoryNode' as const,
                    serverLabel,
                    name,
                    children,
                    totalEntries: this.catalog.all.filter(e => e.category === name).length,
                };
            })
            .filter(c => c.children.length > 0);
    }

    private resolveTools(category: string, config: AutoContextConfig): McpToolsTreeNode[] {
        const toolNames = [...new Set(
            this.catalog.all
                .filter(e => e.category === category)
                .map(e => e.toolName),
        )];

        return toolNames
            .map(toolName => {
                const tasks = this.resolveTasks(toolName, config);
                const isLeaf = tasks.length === 1 && tasks[0].entry.taskName === toolName;
                return {
                    kind: 'mcpToolNode' as const,
                    toolName,
                    category,
                    tasks,
                    isLeaf,
                };
            })
            // A tool with no tasks does nothing, so hide it.
            .filter(n => n.tasks.length > 0)
            .filter(n => this._showNotDetected
                || n.tasks.some(f => f.state !== TreeViewNodeState.NotDetected));
    }

    private resolveTasks(toolName: string, config: AutoContextConfig): McpTaskTreeNode[] {
        return this.catalog.all
            .filter(e => e.toolName === toolName && e.taskName !== undefined)
            .map(entry => ({
                kind: 'mcpTaskNode' as const,
                entry,
                state: this.stateResolver.resolve(entry, config),
            }));
    }

    private getVisibleTasks(node: McpToolsTreeNode): McpTaskTreeNode[] {
        return [...node.tasks]
            .filter(n => this._showNotDetected || n.state !== TreeViewNodeState.NotDetected)
            .sort((a, b) => a.state.sortOrder - b.state.sortOrder);
    }

    private countActive(tools: readonly McpToolsTreeNode[]): number {
        const states: TreeViewNodeState[] = [];
        for (const t of tools) {
            states.push(...t.tasks.map(f => f.state));
        }
        return states.filter(s => s.isActive()).length;
    }

    private checkboxForParent(toolName: string, tasks: readonly McpTaskTreeNode[]): vscode.TreeItemCheckboxState | undefined {
        const detected = tasks.filter(t => t.state !== TreeViewNodeState.NotDetected);
        if (detected.length === 0) { return undefined; }

        const entry = this._config.mcpTools?.[toolName];
        const isDisabled = entry === false || (typeof entry === 'object' && entry.enabled === false);
        return isDisabled
            ? vscode.TreeItemCheckboxState.Unchecked
            : vscode.TreeItemCheckboxState.Checked;
    }

    private serverLabelItem(node: TreeViewServerLabelNode): vscode.TreeItem {
        const item = new vscode.TreeItem(node.name, vscode.TreeItemCollapsibleState.Expanded);
        const active = this.countActive(node.children.flatMap(c => c.children));
        item.tooltip = this.tooltip.container(node.name, active, node.totalEntries);

        const status = this.serverProvider?.getServerStatus(node.name);

        if (status === 'unavailable') {
            item.contextValue = 'serverNode.unavailable';
            item.iconPath = new vscode.ThemeIcon('circle-filled', new vscode.ThemeColor('disabledForeground'));
            item.tooltip = `${item.tooltip}\nNot detected`;
        } else if (status === 'disabled') {
            item.contextValue = 'serverNode.disabled';
            item.iconPath = new vscode.ThemeIcon('circle-filled', new vscode.ThemeColor('disabledForeground'));
            item.tooltip = `${item.tooltip}\nNot active in this workspace`;
        } else if (this.healthMonitor) {
            if (this.healthMonitor.isRunningServerLabel(node.name)) {
                item.contextValue = 'serverNode.running';
                item.iconPath = new vscode.ThemeIcon('circle-filled', new vscode.ThemeColor('testing.iconPassed'));
            } else {
                item.contextValue = 'serverNode.stopped';
                item.iconPath = new vscode.ThemeIcon('circle-filled', new vscode.ThemeColor('testing.iconFailed'));
            }
        } else {
            item.contextValue = 'serverNode.stopped';
        }

        return item;
    }

    private categoryItem(node: McpToolsTreeCategoryNode): vscode.TreeItem {
        const item = new vscode.TreeItem(node.name, vscode.TreeItemCollapsibleState.Expanded);
        item.contextValue = 'categoryNode';
        const active = this.countActive(node.children);
        item.tooltip = this.tooltip.container(node.name, active, node.totalEntries);
        return item;
    }

    private mcpToolItem(node: McpToolsTreeNode): vscode.TreeItem {
        if (node.isLeaf) {
            return this.leafMcpToolItem(node);
        }

        const item = new vscode.TreeItem(node.toolName, vscode.TreeItemCollapsibleState.Expanded);
        item.contextValue = 'mcpToolNode';
        item.checkboxState = this.checkboxForParent(node.toolName, node.tasks);
        const active = node.tasks.filter(f => f.state.isActive()).length;
        const metadata = this.catalog.getMetadata(node.toolName);
        item.tooltip = this.tooltip.container(node.toolName, active, node.tasks.length, metadata?.description);
        return item;
    }

    private leafMcpToolItem(node: McpToolsTreeNode): vscode.TreeItem {
        const task = node.tasks[0];
        const item = new vscode.TreeItem(node.toolName, vscode.TreeItemCollapsibleState.None);
        item.contextValue = 'mcpToolNode';

        if (task.state === TreeViewNodeState.NotDetected) {
            item.iconPath = new vscode.ThemeIcon('circle-slash', new vscode.ThemeColor('disabledForeground'));
            item.description = treeViewLabels.notDetected;
        } else {
            item.checkboxState = task.state.isActive()
                ? vscode.TreeItemCheckboxState.Checked
                : vscode.TreeItemCheckboxState.Unchecked;
        }

        item.tooltip = this.tooltip.leaf(task.entry.label, task.state, task.entry.contextKey, task.entry.description);
        return item;
    }

    private taskItem(node: McpTaskTreeNode): vscode.TreeItem {
        const item = new vscode.TreeItem(node.entry.label, vscode.TreeItemCollapsibleState.None);

        if (node.state === TreeViewNodeState.NotDetected) {
            item.iconPath = new vscode.ThemeIcon('circle-slash', new vscode.ThemeColor('disabledForeground'));
            item.description = treeViewLabels.notDetected;
        } else {
            item.checkboxState = node.state.isActive()
                ? vscode.TreeItemCheckboxState.Checked
                : vscode.TreeItemCheckboxState.Unchecked;
        }

        item.tooltip = this.tooltip.leaf(node.entry.label, node.state, node.entry.contextKey, node.entry.description);
        return item;
    }

    private async handleCheckboxChange(items: ReadonlyArray<readonly [TreeElement, vscode.TreeItemCheckboxState]>): Promise<void> {
        const enabled = (s: vscode.TreeItemCheckboxState) => s === vscode.TreeItemCheckboxState.Checked;

        // When VS Code propagates a parent toggle, both the parent and all its
        // children appear in `items`.  Collect toggled parent tool names so we
        // can skip the children that were only included because of propagation.
        const toggledParents = new Set(
            items
                .filter(([el]) => el.kind === 'mcpToolNode' && (el as McpToolsTreeNode).tasks.length > 0)
                .map(([el]) => (el as McpToolsTreeNode).toolName),
        );

        const updates: Promise<void>[] = [];

        for (const [element, state] of items) {
            if (element.kind === 'mcpTaskNode') {
                if (toggledParents.has(element.entry.toolName)) { continue; }
                updates.push(this.configManager.setMcpToolEnabled(element.entry.toolName, element.entry.taskName, enabled(state)));
                continue;
            }

            if (element.kind !== 'mcpToolNode') { continue; }

            updates.push(this.configManager.setMcpToolEnabled(element.toolName, undefined, enabled(state)));
        }

        await Promise.all(updates);
    }

    dispose(): void {
        for (const d of this.disposables) {
            d.dispose();
        }
    }
}
