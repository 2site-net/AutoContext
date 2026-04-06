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

type TreeElement = GroupNode | CategoryNode | McpToolNode | McpToolFeatureNode;

interface GroupNode {
    readonly kind: 'group';
    readonly name: string;
}

interface CategoryNode {
    readonly kind: 'category';
    readonly group: string;
    readonly name: string;
}

interface McpToolNode {
    readonly kind: 'mcpTool';
    readonly toolName: string;
    readonly category: string;
}

interface McpToolFeatureNode {
    readonly kind: 'mcpToolFeature';
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
            case 'mcpTool': return this.mcpToolItem(element);
            case 'mcpToolFeature': return McpToolsTreeProvider.featureItem(element);
        }
    }

    getChildren(element?: TreeElement): TreeElement[] {
        if (element === undefined) {
            return this.getRootGroups();
        }

        switch (element.kind) {
            case 'group': return this.getCategoriesForGroup(element.name);
            case 'category': return this.getMcpToolsForCategory(element.name);
            case 'mcpTool': return this.getFeaturesForTool(element.toolName);
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
        const hasChildren = (cat: string) => this.getMcpToolsForCategory(cat).length > 0;
        const presentCategories = new Set(
            McpToolsRegistry.all.filter(e => e.group === group).map(e => e.category),
        );
        return categoryOrder
            .filter(c => presentCategories.has(c) && hasChildren(c))
            .map(name => ({ kind: 'category' as const, group, name }));
    }

    private getMcpToolsForCategory(category: string): McpToolNode[] {
        const toolNames = [...new Set(
            McpToolsRegistry.all
                .filter(e => e.category === category)
                .map(e => e.toolName),
        )];

        if (!this._showNotDetected) {
            const config = vscode.workspace.getConfiguration();
            return toolNames
                .filter(toolName => McpToolsRegistry.all
                    .filter(e => e.toolName === toolName)
                    .some(e => McpToolsTreeProvider.resolveState(e, config, this.detector) !== ToolState.NotDetected))
                .map(toolName => ({ kind: 'mcpTool' as const, toolName, category }));
        }

        return toolNames.map(toolName => ({ kind: 'mcpTool' as const, toolName, category }));
    }

    private getResolvedFeatures(toolName: string): McpToolFeatureNode[] {
        const config = vscode.workspace.getConfiguration();
        return McpToolsRegistry.all
            .filter(e => e.toolName === toolName && e.featureName !== undefined)
            .map(entry => ({
                kind: 'mcpToolFeature' as const,
                entry,
                state: McpToolsTreeProvider.resolveState(entry, config, this.detector),
            }));
    }

    private getFeaturesForTool(toolName: string): McpToolFeatureNode[] {
        return this.getResolvedFeatures(toolName)
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

    private static checkboxForParent(features: McpToolFeatureNode[]): vscode.TreeItemCheckboxState | undefined {
        const detected = features.filter(t => t.state !== ToolState.NotDetected);
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

    private mcpToolItem(node: McpToolNode): vscode.TreeItem {
        const features = this.getResolvedFeatures(node.toolName);

        if (features.length === 0) {
            return this.leafMcpToolItem(node);
        }

        const item = new vscode.TreeItem(node.toolName, vscode.TreeItemCollapsibleState.Expanded);
        item.contextValue = 'mcpTool';
        item.checkboxState = McpToolsTreeProvider.checkboxForParent(features);
        item.tooltip = McpToolsTreeProvider.parentTooltip(node.toolName, features);
        return item;
    }

    private leafMcpToolItem(node: McpToolNode): vscode.TreeItem {
        const entry = McpToolsRegistry.all.find(e => e.toolName === node.toolName && !e.featureName)!;
        const config = vscode.workspace.getConfiguration();
        const state = McpToolsTreeProvider.resolveState(entry, config, this.detector);

        const item = new vscode.TreeItem(node.toolName, vscode.TreeItemCollapsibleState.None);
        item.contextValue = 'mcpTool';

        if (state === ToolState.NotDetected) {
            item.iconPath = new vscode.ThemeIcon('circle-slash', new vscode.ThemeColor('disabledForeground'));
            item.description = 'not detected';
        } else {
            item.checkboxState = state === ToolState.Enabled
                ? vscode.TreeItemCheckboxState.Checked
                : vscode.TreeItemCheckboxState.Unchecked;
        }

        item.tooltip = McpToolsTreeProvider.featureTooltip(entry, state);
        return item;
    }

    private static featureItem(node: McpToolFeatureNode): vscode.TreeItem {
        const item = new vscode.TreeItem(node.entry.label, vscode.TreeItemCollapsibleState.None);

        if (node.state === ToolState.NotDetected) {
            item.iconPath = new vscode.ThemeIcon('circle-slash', new vscode.ThemeColor('disabledForeground'));
            item.description = 'not detected';
        } else {
            item.checkboxState = node.state === ToolState.Enabled
                ? vscode.TreeItemCheckboxState.Checked
                : vscode.TreeItemCheckboxState.Unchecked;
        }

        item.tooltip = McpToolsTreeProvider.featureTooltip(node.entry, node.state);
        return item;
    }

    private static featureTooltip(entry: McpToolEntry, state: ToolState): string {
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

    private static parentTooltip(toolName: string, features: McpToolFeatureNode[]): string {
        const detected = features.filter(s => s.state !== ToolState.NotDetected);
        const enabled = detected.filter(s => s.state === ToolState.Enabled).length;
        return `${toolName}\n${enabled}/${detected.length} features enabled`;
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

            const features = this.getResolvedFeatures(element.toolName);

            if (features.length === 0) {
                const entry = McpToolsRegistry.all.find(e => e.toolName === element.toolName && !e.featureName)!;
                updates.push(config.update(entry.settingId, enabled(state), vscode.ConfigurationTarget.Global));
            } else {
                for (const sub of features) {
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
