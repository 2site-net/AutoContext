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

type TreeElement = GroupNode | CategoryNode | ToolNode;

interface GroupNode {
    readonly kind: 'group';
    readonly name: string;
}

interface CategoryNode {
    readonly kind: 'category';
    readonly group: string;
    readonly name: string;
}

interface ToolNode {
    readonly kind: 'tool';
    readonly entry: McpToolEntry;
    readonly state: ToolState;
}

const groupOrder: readonly string[] = ['Languages', 'Platforms', 'Workspace'];
const categoryOrder: readonly string[] = ['C#', 'TypeScript', '.NET', 'Git', 'EditorConfig'];

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
    private readonly disposables: vscode.Disposable[] = [];

    constructor(private readonly detector: WorkspaceContextDetector) {
        const treeView = vscode.window.createTreeView(McpToolsTreeProvider.viewId, {
            treeDataProvider: this,
            manageCheckboxStateManually: true,
        });

        this.disposables.push(
            treeView,
            this._onDidChangeTreeData,
            detector.onDidDetect(() => this.refresh()),
            vscode.workspace.onDidChangeConfiguration(e => {
                if (e.affectsConfiguration('sharppilot.tools')) {
                    this.refresh();
                }
            }),
            treeView.onDidChangeCheckboxState(e => {
                void McpToolsTreeProvider.handleCheckboxChange(e.items);
            }),
        );
    }

    refresh(): void {
        this._onDidChangeTreeData.fire(undefined);
    }

    getTreeItem(element: TreeElement): vscode.TreeItem {
        if (element.kind === 'group') {
            return McpToolsTreeProvider.groupItem(element);
        }

        if (element.kind === 'category') {
            return McpToolsTreeProvider.categoryItem(element);
        }

        return McpToolsTreeProvider.toolItem(element);
    }

    getChildren(element?: TreeElement): TreeElement[] {
        if (element === undefined) {
            return this.getRootGroups();
        }

        if (element.kind === 'group') {
            return this.getCategoriesForGroup(element.name);
        }

        if (element.kind === 'category') {
            return this.getToolsForCategory(element.name);
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

    private getRootGroups(): GroupNode[] {
        const hasChildren = (group: string) => this.getCategoriesForGroup(group).length > 0;
        const presentGroups = new Set(McpToolsRegistry.all.map(e => e.group));
        return groupOrder
            .filter(g => presentGroups.has(g) && hasChildren(g))
            .map(name => ({ kind: 'group' as const, name }));
    }

    private getCategoriesForGroup(group: string): CategoryNode[] {
        const hasChildren = (cat: string) => this.getToolsForCategory(cat).length > 0;
        const presentCategories = new Set(
            McpToolsRegistry.all.filter(e => e.group === group).map(e => e.category),
        );
        return categoryOrder
            .filter(c => presentCategories.has(c) && hasChildren(c))
            .map(name => ({ kind: 'category' as const, group, name }));
    }

    private getToolsForCategory(category: string): ToolNode[] {
        const config = vscode.workspace.getConfiguration();

        return McpToolsRegistry.all
            .filter(e => e.category === category)
            .map(entry => ({
                kind: 'tool' as const,
                entry,
                state: McpToolsTreeProvider.resolveState(entry, config, this.detector),
            }))
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

    private static toolItem(node: ToolNode): vscode.TreeItem {
        const item = new vscode.TreeItem(node.entry.label, vscode.TreeItemCollapsibleState.None);

        if (node.state === ToolState.NotDetected) {
            item.iconPath = new vscode.ThemeIcon('circle-slash', new vscode.ThemeColor('disabledForeground'));
            item.description = 'not detected';
        } else {
            item.checkboxState = node.state === ToolState.Enabled
                ? vscode.TreeItemCheckboxState.Checked
                : vscode.TreeItemCheckboxState.Unchecked;
        }

        item.tooltip = McpToolsTreeProvider.tooltip(node);
        return item;
    }

    private static tooltip(node: ToolNode): string {
        const lines = [node.entry.label];

        switch (node.state) {
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
                const _: never = node.state;
                void _;
                break;
            }
        }

        lines.push(`Setting: ${node.entry.settingId}`);
        return lines.join('\n');
    }

    private static async handleCheckboxChange(items: ReadonlyArray<readonly [TreeElement, vscode.TreeItemCheckboxState]>): Promise<void> {
        const config = vscode.workspace.getConfiguration();

        for (const [element, state] of items) {
            if (element.kind !== 'tool') { continue; }

            const enabled = state === vscode.TreeItemCheckboxState.Checked;
            await config.update(element.entry.settingId, enabled, vscode.ConfigurationTarget.Global);
        }
    }

    dispose(): void {
        for (const d of this.disposables) {
            d.dispose();
        }
    }
}
