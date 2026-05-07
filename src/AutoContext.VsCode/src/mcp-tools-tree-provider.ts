import * as vscode from 'vscode';
import type { McpToolsManifest } from './mcp-tools-manifest.js';
import type { McpToolEntry } from './mcp-tool-entry.js';
import type { McpCategoryEntry } from './mcp-category-entry.js';
import { TreeViewNodeState } from './tree-view-node-state.js';
import { viewIds, treeViewLabels, mcpServerNodeLabels } from './ui-constants.js';
import type { TreeViewTooltip } from './tree-view-tooltip.js';
import type { HealthMonitorServer } from './health-monitor-server.js';
import type { McpServerProvider } from './mcp-server-provider.js';
import type { McpToolsTreeTopCategoryNode } from '#types/mcp-tools-tree-top-category-node.js';
import type { McpToolsTreeSubCategoryNode } from '#types/mcp-tools-tree-sub-category-node.js';
import type { McpToolsTreeNode } from '#types/mcp-tools-tree-node.js';
import type { McpTaskTreeNode } from '#types/mcp-task-tree-node.js';
import type { McpServerTreeNode } from '#types/mcp-server-tree-node.js';
import type { AutoContextConfigManager } from './autocontext-config-manager.js';
import type { ChannelLogger } from 'autocontext-framework-web';
import type { McpToolsTreeProviderOptions } from '#types/mcp-tools-tree-provider-options.js';

type TreeElement = McpServerTreeNode | McpToolsTreeTopCategoryNode | McpToolsTreeSubCategoryNode | McpToolsTreeNode | McpTaskTreeNode;

/**
 * Health pipe id used by `AutoContext.Mcp.Server` (matches
 * `HealthClientId` constant in the server's `Program.cs`).
 */
const MCP_SERVER_HEALTH_ID = 'mcp-server';

/**
 * Logical name passed to the `autocontext.show-mcp-server-output`
 * command handler so it can resolve the VS Code MCP definition id.
 */
const MCP_SERVER_DISPLAY_NAME = 'AutoContext.Mcp.Server';

export class McpToolsTreeProvider implements vscode.TreeDataProvider<TreeElement>, vscode.Disposable {

    private readonly _onDidChangeTreeData = new vscode.EventEmitter<TreeElement | undefined>();
    readonly onDidChangeTreeData = this._onDidChangeTreeData.event;
    private _showNotDetected = true;
    private readonly treeView: vscode.TreeView<TreeElement>;
    private readonly disposables: vscode.Disposable[] = [];

    private readonly manifest: McpToolsManifest;
    private readonly tooltip: TreeViewTooltip;
    private readonly configManager: AutoContextConfigManager;
    private readonly logger: ChannelLogger;
    private readonly healthMonitor?: HealthMonitorServer;
    private readonly serverProvider?: McpServerProvider;

    constructor(options: McpToolsTreeProviderOptions) {
        const {
            detector,
            manifest,
            tooltip,
            configManager,
            logger,
            healthMonitor,
            serverProvider,
        } = options;

        this.manifest = manifest;
        this.tooltip = tooltip;
        this.configManager = configManager;
        this.logger = logger;
        this.healthMonitor = healthMonitor;
        this.serverProvider = serverProvider;

        this.treeView = vscode.window.createTreeView(viewIds.Tools, {
            treeDataProvider: this,
            manageCheckboxStateManually: true,
        });

        this.updateDescription();

        this.disposables.push(
            this.treeView,
            this._onDidChangeTreeData,
            detector.onDidDetect(() => this.refresh()),
            configManager.onDidChange(() => this.refresh()),
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
        const states = allTasks.map(t => t.resolveState());
        this.treeView.description = this.tooltip.description(states.filter(s => s.isActive()).length, allTasks.length);
    }

    getTreeItem(element: TreeElement): vscode.TreeItem {
        switch (element.kind) {
            case 'mcpServerNode': return this.mcpServerItem(element);
            case 'mcpTopCategoryNode': return this.mcpTopCategoryItem(element);
            case 'mcpSubCategoryNode': return this.mcpSubCategoryItem(element);
            case 'mcpToolNode': return this.mcpToolItem(element);
            case 'mcpTaskNode': return this.taskItem(element);
        }
    }

    getChildren(element?: TreeElement): TreeElement[] {
        if (element === undefined) {
            return [
                { kind: 'mcpServerNode', name: MCP_SERVER_DISPLAY_NAME },
                ...this.buildTree(),
            ];
        }

        switch (element.kind) {
            case 'mcpTopCategoryNode': return [...element.children];
            case 'mcpSubCategoryNode': return [...element.children];
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

    private buildTree(): McpToolsTreeTopCategoryNode[] {
        return this.manifest.topCategories
            .map(topCat => {
                const children = this.resolveSubCategories(topCat);
                const totalEntries = this.manifest.tools
                    .filter(t => t.firstCategory === topCat)
                    .reduce((sum, t) => sum + t.tasks.length, 0);
                return {
                    kind: 'mcpTopCategoryNode' as const,
                    name: topCat.name,
                    workerId: topCat.workerId,
                    children,
                    totalEntries,
                };
            })
            .filter(g => g.children.length > 0);
    }

    private resolveSubCategories(topCat: McpCategoryEntry): McpToolsTreeSubCategoryNode[] {
        const toolsUnder = this.manifest.tools.filter(t => t.firstCategory === topCat);
        const subCatsUsed = new Set(toolsUnder.map(t => t.lastCategory));

        return this.manifest.subCategories
            .filter(sc => subCatsUsed.has(sc))
            .map(subCat => {
                const toolsInSubCat = toolsUnder.filter(t => t.lastCategory === subCat);
                const children = this.resolveTools(toolsInSubCat, subCat);
                const totalEntries = toolsInSubCat.reduce((sum, t) => sum + t.tasks.length, 0);
                return {
                    kind: 'mcpSubCategoryNode' as const,
                    name: subCat.name,
                    children,
                    totalEntries,
                };
            })
            .filter(c => c.children.length > 0);
    }

    private resolveTools(tools: readonly McpToolEntry[], subCat: McpCategoryEntry): McpToolsTreeNode[] {
        return tools
            .map(tool => {
                const tasks = this.resolveTasks(tool);
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

    private resolveTasks(tool: McpToolEntry): McpTaskTreeNode[] {
        return tool.tasks.map(task => ({
            kind: 'mcpTaskNode' as const,
            task,
            state: task.resolveState(),
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

        const entry = this.configManager.readSync().mcpTools?.[toolName];
        const isDisabled = entry === false || (typeof entry === 'object' && entry.enabled === false);
        return isDisabled
            ? vscode.TreeItemCheckboxState.Unchecked
            : vscode.TreeItemCheckboxState.Checked;
    }

    private mcpServerItem(_node: McpServerTreeNode): vscode.TreeItem {
        const item = new vscode.TreeItem(mcpServerNodeLabels.label, vscode.TreeItemCollapsibleState.None);
        item.id = 'autocontext.mcp-server-node';

        // Four-state status (gray / green / red — no transient "idle"
        // because VS Code starts MCP servers eagerly to enumerate
        // tools, so the gap between "definition registered" and
        // "process connected" is too short to be visible):
        //   unavailable  - Mcp.Server binary missing on disk (gray)
        //   disabled     - binary exists, no tools enabled (gray)
        //   running      - health pipe reports the server connected (green)
        //   stopped      - server should be running but is not (red)
        const availability = this.serverProvider?.getServerStatus(MCP_SERVER_DISPLAY_NAME) ?? 'available';
        const isRunning = this.healthMonitor?.isRunning(MCP_SERVER_HEALTH_ID) ?? false;

        let state: 'unavailable' | 'disabled' | 'running' | 'stopped';
        let statusLabel: string;
        let iconColor: string;

        if (availability === 'unavailable') {
            state = 'unavailable';
            statusLabel = mcpServerNodeLabels.statusUnavailable;
            iconColor = 'disabledForeground';
        } else if (availability === 'disabled') {
            state = 'disabled';
            statusLabel = mcpServerNodeLabels.statusDisabled;
            iconColor = 'disabledForeground';
        } else if (isRunning) {
            state = 'running';
            statusLabel = mcpServerNodeLabels.statusRunning;
            iconColor = 'testing.iconPassed';
        } else {
            state = 'stopped';
            statusLabel = mcpServerNodeLabels.statusStopped;
            iconColor = 'testing.iconFailed';
        }

        item.iconPath = new vscode.ThemeIcon('circle-filled', new vscode.ThemeColor(iconColor));
        item.contextValue = `mcpServerNode.${state}`;

        const tooltip = new vscode.MarkdownString(undefined, true);
        tooltip.appendMarkdown(`**${mcpServerNodeLabels.label}**\n\n`);
        tooltip.appendMarkdown(`${mcpServerNodeLabels.description}\n\n`);
        tooltip.appendMarkdown(`**Status:** ${statusLabel}\n\n`);
        tooltip.appendMarkdown(`**Health pipe id:** \`${MCP_SERVER_HEALTH_ID}\``);
        if (state === 'stopped') {
            tooltip.appendMarkdown(`\n\n${mcpServerNodeLabels.notConnectedHint}`);
        }
        item.tooltip = tooltip;

        // Row click is intentionally inert; actions are driven by the
        // inline "Show Output" button declared in `view/item/context`.
        return item;
    }

    private mcpTopCategoryItem(node: McpToolsTreeTopCategoryNode): vscode.TreeItem {
        const item = new vscode.TreeItem(node.name, vscode.TreeItemCollapsibleState.Expanded);
        const active = this.countActive(node.children.flatMap(c => c.children));
        item.tooltip = this.tooltip.container(node.name, active, node.totalEntries);

        const status = this.serverProvider?.getServerStatus(node.name);

        if (status === 'unavailable') {
            item.contextValue = 'mcpTopCategoryNode.unavailable';
            item.iconPath = new vscode.ThemeIcon('circle-filled', new vscode.ThemeColor('disabledForeground'));
            item.tooltip = `${item.tooltip}\nNot detected`;
        } else if (status === 'disabled') {
            item.contextValue = 'mcpTopCategoryNode.disabled';
            item.iconPath = new vscode.ThemeIcon('circle-filled', new vscode.ThemeColor('disabledForeground'));
            item.tooltip = `${item.tooltip}\nNot active in this workspace`;
        } else if (this.healthMonitor) {
            if (node.workerId !== undefined && this.healthMonitor.isRunning(node.workerId)) {
                item.contextValue = 'mcpTopCategoryNode.running';
                item.iconPath = new vscode.ThemeIcon('circle-filled', new vscode.ThemeColor('testing.iconPassed'));
            } else {
                item.contextValue = 'mcpTopCategoryNode.stopped';
                item.iconPath = new vscode.ThemeIcon('circle-filled', new vscode.ThemeColor('testing.iconFailed'));
            }
        } else {
            item.contextValue = 'mcpTopCategoryNode.stopped';
        }

        return item;
    }

    private mcpSubCategoryItem(node: McpToolsTreeSubCategoryNode): vscode.TreeItem {
        const item = new vscode.TreeItem(node.name, vscode.TreeItemCollapsibleState.Expanded);
        item.contextValue = 'mcpSubCategoryNode';
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
