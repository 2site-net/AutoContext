import * as vscode from 'vscode';
import type { McpToolsManifest } from './mcp-tools-manifest.js';
import type { McpToolEntry } from './mcp-tool-entry.js';
import type { McpCategoryEntry } from './mcp-category-entry.js';
import { TreeViewNodeState } from './tree-view-node-state.js';
import { viewIds, treeViewLabels } from './ui-constants.js';
import type { WorkspaceContextDetector } from './workspace-context-detector.js';
import type { TreeViewStateResolver } from './tree-view-state-resolver.js';
import type { TreeViewTooltip } from './tree-view-tooltip.js';
import type { HealthMonitorServer } from './health-monitor.js';
import type { McpServerProvider } from './mcp-server-provider.js';
import type { TreeViewServerLabelNode } from '#types/tree-view-server-label-node.js';
import type { McpToolsTreeCategoryNode } from '#types/mcp-tools-tree-category-node.js';
import type { McpToolsTreeNode } from '#types/mcp-tools-tree-node.js';
import type { McpTaskTreeNode } from '#types/mcp-task-tree-node.js';
import type { AutoContextConfigManager } from './autocontext-config.js';
import type { AutoContextConfig } from '#types/autocontext-config.js';
import type { Logger } from '#types/logger.js';

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
        private readonly manifest: McpToolsManifest,
        private readonly stateResolver: TreeViewStateResolver,
        private readonly tooltip: TreeViewTooltip,
        private readonly configManager: AutoContextConfigManager,
        private readonly logger: Logger,
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
                    this.logger.error('Failed to update config', err),
                );
            }),
            this.treeView.onDidChangeCheckboxState(e => {
                void this.handleCheckboxChange(e.items).catch(err =>
                    this.logger.error('Failed to handle checkbox change', err),
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
        const allTasks = this.manifest.tools.flatMap(t => t.tasks);
        const states = allTasks.map(t => this.stateResolver.resolveTask(t.tool, t, this._config));
        this.treeView.description = this.tooltip.description(states.filter(s => s.isActive()).length, allTasks.length);
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
        return this.manifest.topCategories
            .map(topCat => {
                const children = this.resolveCategories(topCat, this._config);
                const totalEntries = this.manifest.tools
                    .filter(t => t.firstCategory === topCat)
                    .reduce((sum, t) => sum + t.tasks.length, 0);
                return {
                    kind: 'serverNode' as const,
                    name: topCat.name,
                    workerId: topCat.workerId,
                    children,
                    totalEntries,
                };
            })
            .filter(g => g.children.length > 0);
    }

    private resolveCategories(topCat: McpCategoryEntry, config: AutoContextConfig): McpToolsTreeCategoryNode[] {
        const toolsUnder = this.manifest.tools.filter(t => t.firstCategory === topCat);
        const subCatsUsed = new Set(toolsUnder.map(t => t.lastCategory));

        return this.manifest.subCategories
            .filter(sc => subCatsUsed.has(sc))
            .map(subCat => {
                const toolsInSubCat = toolsUnder.filter(t => t.lastCategory === subCat);
                const children = this.resolveTools(toolsInSubCat, subCat, config);
                const totalEntries = toolsInSubCat.reduce((sum, t) => sum + t.tasks.length, 0);
                return {
                    kind: 'categoryNode' as const,
                    serverLabel: topCat.name,
                    name: subCat.name,
                    children,
                    totalEntries,
                };
            })
            .filter(c => c.children.length > 0);
    }

    private resolveTools(tools: readonly McpToolEntry[], subCat: McpCategoryEntry, config: AutoContextConfig): McpToolsTreeNode[] {
        return tools
            .map(tool => {
                const tasks = this.resolveTasks(tool, config);
                const isLeaf = tasks.length === 1 && tasks[0].task.name === tool.name;
                return {
                    kind: 'mcpToolNode' as const,
                    tool,
                    category: subCat.name,
                    tasks,
                    isLeaf,
                };
            })
            .filter(n => n.tasks.length > 0)
            .filter(n => this._showNotDetected
                || n.tasks.some(f => f.state !== TreeViewNodeState.NotDetected));
    }

    private resolveTasks(tool: McpToolEntry, config: AutoContextConfig): McpTaskTreeNode[] {
        return tool.tasks.map(task => ({
            kind: 'mcpTaskNode' as const,
            task,
            state: this.stateResolver.resolveTask(tool, task, config),
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
            if (node.workerId !== undefined && this.healthMonitor.isRunning(node.workerId)) {
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

        const item = new vscode.TreeItem(node.tool.name, vscode.TreeItemCollapsibleState.Expanded);
        item.contextValue = 'mcpToolNode';
        item.checkboxState = this.checkboxForParent(node.tool.name, node.tasks);
        const active = node.tasks.filter(f => f.state.isActive()).length;
        item.tooltip = this.tooltip.container(node.tool.name, active, node.tasks.length, node.tool.description);
        return item;
    }

    private leafMcpToolItem(node: McpToolsTreeNode): vscode.TreeItem {
        const task = node.tasks[0];
        const item = new vscode.TreeItem(node.tool.name, vscode.TreeItemCollapsibleState.None);
        item.contextValue = 'mcpToolNode';

        if (task.state === TreeViewNodeState.NotDetected) {
            item.iconPath = new vscode.ThemeIcon('circle-slash', new vscode.ThemeColor('disabledForeground'));
            item.description = treeViewLabels.notDetected;
        } else {
            item.checkboxState = task.state.isActive()
                ? vscode.TreeItemCheckboxState.Checked
                : vscode.TreeItemCheckboxState.Unchecked;
        }

        item.tooltip = this.tooltip.leaf(task.task.name, task.state, task.task.runtimeInfo.contextKey, task.task.description);
        return item;
    }

    private taskItem(node: McpTaskTreeNode): vscode.TreeItem {
        const item = new vscode.TreeItem(node.task.name, vscode.TreeItemCollapsibleState.None);

        if (node.state === TreeViewNodeState.NotDetected) {
            item.iconPath = new vscode.ThemeIcon('circle-slash', new vscode.ThemeColor('disabledForeground'));
            item.description = treeViewLabels.notDetected;
        } else {
            item.checkboxState = node.state.isActive()
                ? vscode.TreeItemCheckboxState.Checked
                : vscode.TreeItemCheckboxState.Unchecked;
        }

        item.tooltip = this.tooltip.leaf(node.task.name, node.state, node.task.runtimeInfo.contextKey, node.task.description);
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
                .map(([el]) => (el as McpToolsTreeNode).tool.name),
        );

        const updates: Promise<void>[] = [];

        for (const [element, state] of items) {
            if (element.kind === 'mcpTaskNode') {
                if (toggledParents.has(element.task.tool.name)) { continue; }
                updates.push(this.configManager.setMcpToolEnabled(element.task.tool.name, element.task.name, enabled(state)));
                continue;
            }

            if (element.kind !== 'mcpToolNode') { continue; }

            updates.push(this.configManager.setMcpToolEnabled(element.tool.name, undefined, enabled(state)));
        }

        await Promise.all(updates);
    }

    dispose(): void {
        for (const d of this.disposables) {
            d.dispose();
        }
    }
}
