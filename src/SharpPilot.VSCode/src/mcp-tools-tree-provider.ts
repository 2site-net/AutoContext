import * as vscode from 'vscode';
import { McpToolsRegistry } from './mcp-tools-registry.js';
import { ContextKeys } from './context-keys.js';
import type { WorkspaceContextDetector } from './workspace-context-detector.js';
import type { McpToolEntry } from './mcp-tool-entry.js';

export const ToolState = {
    Enabled: 'enabled',
    Disabled: 'disabled',
    NotDetected: 'notDetected',
} as const;

export type ToolState = typeof ToolState[keyof typeof ToolState];

type TreeElement = GroupNode | CategoryNode | AggregationToolNode | SubCheckNode;

interface GroupNode {
    readonly kind: 'group';
    readonly name: string;
}

interface CategoryNode {
    readonly kind: 'category';
    readonly group: string;
    readonly name: string;
}

interface AggregationToolNode {
    readonly kind: 'aggregationTool';
    readonly toolName: string;
    readonly category: string;
}

interface SubCheckNode {
    readonly kind: 'subCheck';
    readonly entry: McpToolEntry;
    readonly state: ToolState;
}

const groupOrder: readonly string[] = ['.NET', 'Web', 'Workspace'];
const categoryOrder: readonly string[] = ['C#', 'NuGet', 'TypeScript', 'Git', 'EditorConfig'];

const stateRank: Record<ToolState, number> = {
    [ToolState.Enabled]: 0,
    [ToolState.Disabled]: 1,
    [ToolState.NotDetected]: 2,
};

export class McpToolsTreeProvider implements vscode.TreeDataProvider<TreeElement>, vscode.Disposable {
    static readonly viewId = 'sharppilot.toolsView';

    private readonly _onDidChangeTreeData = new vscode.EventEmitter<TreeElement | undefined>();
    readonly onDidChangeTreeData = this._onDidChangeTreeData.event;
    private _showNotDetected = true;
    private readonly treeView: vscode.TreeView<TreeElement>;
    private readonly disposables: vscode.Disposable[] = [];

    constructor(private readonly detector: WorkspaceContextDetector) {
        this.treeView = vscode.window.createTreeView(McpToolsTreeProvider.viewId, {
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
        const enabled = McpToolsRegistry.all.filter(e => config.get<boolean>(e.settingId, true)).length;
        this.treeView.description = `${enabled}/${McpToolsRegistry.count}`;
    }

    getTreeItem(element: TreeElement): vscode.TreeItem {
        switch (element.kind) {
            case 'group': return McpToolsTreeProvider.groupItem(element);
            case 'category': return McpToolsTreeProvider.categoryItem(element);
            case 'aggregationTool': return this.aggregationToolItem(element);
            case 'subCheck': return McpToolsTreeProvider.subCheckItem(element);
        }
    }

    getChildren(element?: TreeElement): TreeElement[] {
        if (element === undefined) {
            return this.getRootGroups();
        }

        switch (element.kind) {
            case 'group': return this.getCategoriesForGroup(element.name);
            case 'category': return this.getAggregationToolsForCategory(element.name);
            case 'aggregationTool': return this.getSubChecksForTool(element.toolName);
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

    private getRootGroups(): GroupNode[] {
        const hasChildren = (group: string) => this.getCategoriesForGroup(group).length > 0;
        const presentGroups = new Set(McpToolsRegistry.all.map(e => e.group));
        return groupOrder
            .filter(g => presentGroups.has(g) && hasChildren(g))
            .map(name => ({ kind: 'group' as const, name }));
    }

    private getCategoriesForGroup(group: string): CategoryNode[] {
        const hasChildren = (cat: string) => this.getAggregationToolsForCategory(cat).length > 0;
        const presentCategories = new Set(
            McpToolsRegistry.all.filter(e => e.group === group).map(e => e.category),
        );
        return categoryOrder
            .filter(c => presentCategories.has(c) && hasChildren(c))
            .map(name => ({ kind: 'category' as const, group, name }));
    }

    private getAggregationToolsForCategory(category: string): AggregationToolNode[] {
        const toolNames = [...new Set(
            McpToolsRegistry.all
                .filter(e => e.category === category)
                .map(e => e.aggregationTool),
        )];

        if (!this._showNotDetected) {
            const config = vscode.workspace.getConfiguration();
            return toolNames
                .filter(toolName => McpToolsRegistry.all
                    .filter(e => e.aggregationTool === toolName)
                    .some(e => McpToolsTreeProvider.resolveState(e, config, this.detector) !== ToolState.NotDetected))
                .map(toolName => ({ kind: 'aggregationTool' as const, toolName, category }));
        }

        return toolNames.map(toolName => ({ kind: 'aggregationTool' as const, toolName, category }));
    }

    private getResolvedSubChecks(toolName: string): SubCheckNode[] {
        const config = vscode.workspace.getConfiguration();
        return McpToolsRegistry.all
            .filter(e => e.aggregationTool === toolName && e.toolName !== toolName)
            .map(entry => ({
                kind: 'subCheck' as const,
                entry,
                state: McpToolsTreeProvider.resolveState(entry, config, this.detector),
            }));
    }

    private getSubChecksForTool(toolName: string): SubCheckNode[] {
        return this.getResolvedSubChecks(toolName)
            .filter(n => this._showNotDetected || n.state !== ToolState.NotDetected)
            .sort((a, b) => stateRank[a.state] - stateRank[b.state]);
    }

    private static resolveState(
        entry: McpToolEntry,
        config: vscode.WorkspaceConfiguration,
        detector: WorkspaceContextDetector,
    ): ToolState {
        const ctxKeys = ContextKeys.forEntry(entry);
        if (ctxKeys.length > 0 && !ctxKeys.some(k => detector.get(k))) {
            return ToolState.NotDetected;
        }

        if (!config.get<boolean>(entry.settingId, true)) {
            return ToolState.Disabled;
        }

        return ToolState.Enabled;
    }

    private static checkboxForParent(subChecks: SubCheckNode[]): vscode.TreeItemCheckboxState | undefined {
        const detected = subChecks.filter(t => t.state !== ToolState.NotDetected);
        if (detected.length === 0) { return undefined; }

        return detected.every(t => t.state === ToolState.Enabled)
            ? vscode.TreeItemCheckboxState.Checked
            : vscode.TreeItemCheckboxState.Unchecked;
    }

    private static groupItem(node: GroupNode): vscode.TreeItem {
        const item = new vscode.TreeItem(node.name, vscode.TreeItemCollapsibleState.Expanded);
        item.contextValue = 'group';
        return item;
    }

    private static categoryItem(node: CategoryNode): vscode.TreeItem {
        const item = new vscode.TreeItem(node.name, vscode.TreeItemCollapsibleState.Expanded);
        item.contextValue = 'category';
        return item;
    }

    private aggregationToolItem(node: AggregationToolNode): vscode.TreeItem {
        const subChecks = this.getResolvedSubChecks(node.toolName);

        if (subChecks.length === 0) {
            return this.leafAggregationToolItem(node);
        }

        const item = new vscode.TreeItem(node.toolName, vscode.TreeItemCollapsibleState.Expanded);
        item.contextValue = 'aggregationTool';
        item.checkboxState = McpToolsTreeProvider.checkboxForParent(subChecks);
        item.tooltip = McpToolsTreeProvider.parentTooltip(node.toolName, subChecks);
        return item;
    }

    private leafAggregationToolItem(node: AggregationToolNode): vscode.TreeItem {
        const entry = McpToolsRegistry.all.find(e => e.aggregationTool === node.toolName && e.toolName === node.toolName)!;
        const config = vscode.workspace.getConfiguration();
        const state = McpToolsTreeProvider.resolveState(entry, config, this.detector);

        const item = new vscode.TreeItem(node.toolName, vscode.TreeItemCollapsibleState.None);
        item.contextValue = 'aggregationTool';

        if (state === ToolState.NotDetected) {
            item.iconPath = new vscode.ThemeIcon('circle-slash', new vscode.ThemeColor('disabledForeground'));
            item.description = 'not detected';
        } else {
            item.checkboxState = state === ToolState.Enabled
                ? vscode.TreeItemCheckboxState.Checked
                : vscode.TreeItemCheckboxState.Unchecked;
        }

        item.tooltip = McpToolsTreeProvider.subCheckTooltip(entry, state);
        return item;
    }

    private static subCheckItem(node: SubCheckNode): vscode.TreeItem {
        const item = new vscode.TreeItem(node.entry.label, vscode.TreeItemCollapsibleState.None);

        if (node.state === ToolState.NotDetected) {
            item.iconPath = new vscode.ThemeIcon('circle-slash', new vscode.ThemeColor('disabledForeground'));
            item.description = 'not detected';
        } else {
            item.checkboxState = node.state === ToolState.Enabled
                ? vscode.TreeItemCheckboxState.Checked
                : vscode.TreeItemCheckboxState.Unchecked;
        }

        item.tooltip = McpToolsTreeProvider.subCheckTooltip(node.entry, node.state);
        return item;
    }

    private static subCheckTooltip(entry: McpToolEntry, state: ToolState): string {
        const lines = [entry.label];

        switch (state) {
            case ToolState.Enabled:
                lines.push('Enabled — available to Copilot');
                break;
            case ToolState.Disabled:
                lines.push('Disabled — turned off in settings');
                break;
            case ToolState.NotDetected:
                lines.push('Not detected — workspace lacks matching files');
                break;
            default: {
                const _: never = state;
                void _;
                break;
            }
        }

        lines.push(`Setting: ${entry.settingId}`);
        return lines.join('\n');
    }

    private static parentTooltip(toolName: string, subChecks: SubCheckNode[]): string {
        const detected = subChecks.filter(s => s.state !== ToolState.NotDetected);
        const enabled = detected.filter(s => s.state === ToolState.Enabled).length;
        return `${toolName}\n${enabled}/${detected.length} sub-checks enabled`;
    }

    private async handleCheckboxChange(items: ReadonlyArray<readonly [TreeElement, vscode.TreeItemCheckboxState]>): Promise<void> {
        const config = vscode.workspace.getConfiguration();
        const enabled = (s: vscode.TreeItemCheckboxState) => s === vscode.TreeItemCheckboxState.Checked;

        const updates: Thenable<void>[] = [];

        for (const [element, state] of items) {
            if (element.kind === 'subCheck') {
                updates.push(config.update(element.entry.settingId, enabled(state), vscode.ConfigurationTarget.Global));
                continue;
            }

            if (element.kind !== 'aggregationTool') { continue; }

            const subChecks = this.getResolvedSubChecks(element.toolName);

            if (subChecks.length === 0) {
                const entry = McpToolsRegistry.all.find(e => e.aggregationTool === element.toolName && e.toolName === element.toolName)!;
                updates.push(config.update(entry.settingId, enabled(state), vscode.ConfigurationTarget.Global));
            } else {
                for (const sub of subChecks) {
                    if (sub.state === ToolState.NotDetected) { continue; }
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
