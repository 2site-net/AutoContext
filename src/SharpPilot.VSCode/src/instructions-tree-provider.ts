import * as vscode from 'vscode';
import { InstructionsRegistry } from './instructions-registry.js';
import { ContextKeys } from './context-keys.js';
import { instructionScheme } from './instructions-content-provider.js';
import type { WorkspaceContextDetector } from './workspace-context-detector.js';
import type { InstructionsCatalogEntry } from './instructions-catalog-entry.js';

export const InstructionState = {
    Active: 'active',
    NotDetected: 'notDetected',
    Disabled: 'disabled',
    Overridden: 'overridden',
} as const;

export type InstructionState = typeof InstructionState[keyof typeof InstructionState];

type TreeElement = CategoryNode | InstructionNode;

interface CategoryNode {
    readonly kind: 'category';
    readonly name: string;
}

interface InstructionNode {
    readonly kind: 'instruction';
    readonly entry: InstructionsCatalogEntry;
    readonly state: InstructionState;
}

// Category order: General → Languages → Platforms (.NET, Web) → Tools.
const categoryOrder: readonly string[] = ['General', 'Languages', '.NET', 'Web', 'Tools'];

// Sort rank: Active & Overridden first, then Disabled, then NotDetected.
const stateRank: Record<InstructionState, number> = {
    [InstructionState.Active]: 0,
    [InstructionState.Overridden]: 1,
    [InstructionState.Disabled]: 2,
    [InstructionState.NotDetected]: 3,
};

export class InstructionsTreeProvider implements vscode.TreeDataProvider<TreeElement>, vscode.Disposable {
    static readonly viewId = 'sharppilot.instructionsView';
    static readonly enableCommandId = 'sharppilot.enableInstruction';
    static readonly disableCommandId = 'sharppilot.disableInstruction';

    private readonly _onDidChangeTreeData = new vscode.EventEmitter<TreeElement | undefined>();
    readonly onDidChangeTreeData = this._onDidChangeTreeData.event;

    private readonly disposables: vscode.Disposable[] = [];

    constructor(private readonly detector: WorkspaceContextDetector) {
        this.disposables.push(
            this._onDidChangeTreeData,
            detector.onDidDetect(() => this.refresh()),
            vscode.workspace.onDidChangeConfiguration(e => {
                if (e.affectsConfiguration('sharppilot.instructions')) {
                    this.refresh();
                }
            }),
        );
    }

    refresh(): void {
        this._onDidChangeTreeData.fire(undefined);
    }

    getTreeItem(element: TreeElement): vscode.TreeItem {
        if (element.kind === 'category') {
            return InstructionsTreeProvider.categoryItem(element);
        }

        return InstructionsTreeProvider.instructionItem(element);
    }

    getChildren(element?: TreeElement): TreeElement[] {
        if (element === undefined) {
            return this.getRootCategories();
        }

        if (element.kind === 'category') {
            return this.getInstructionsForCategory(element.name);
        }

        return [];
    }

    private getRootCategories(): CategoryNode[] {
        const presentCategories = new Set(InstructionsRegistry.all.map(e => e.category));
        return categoryOrder
            .filter(c => presentCategories.has(c))
            .map(name => ({ kind: 'category' as const, name }));
    }

    private getInstructionsForCategory(category: string): InstructionNode[] {
        const config = vscode.workspace.getConfiguration();
        const overrides = this.detector.getOverriddenSettingIds();

        return InstructionsRegistry.all
            .filter(e => e.category === category)
            .map(entry => ({
                kind: 'instruction' as const,
                entry,
                state: InstructionsTreeProvider.resolveState(entry, config, this.detector, overrides),
            }))
            .sort((a, b) => stateRank[a.state] - stateRank[b.state]);
    }

    private static resolveState(
        entry: InstructionsCatalogEntry,
        config: vscode.WorkspaceConfiguration,
        detector: WorkspaceContextDetector,
        overrides: ReadonlySet<string>,
    ): InstructionState {
        const ctxKeys = ContextKeys.forEntry(entry);
        if (ctxKeys.length > 0 && !ctxKeys.some(k => detector.get(k))) {
            return InstructionState.NotDetected;
        }

        if (!config.get<boolean>(entry.settingId, true)) {
            return InstructionState.Disabled;
        }

        if (overrides.has(entry.settingId)) {
            return InstructionState.Overridden;
        }

        return InstructionState.Active;
    }

    private static categoryItem(node: CategoryNode): vscode.TreeItem {
        const item = new vscode.TreeItem(node.name, vscode.TreeItemCollapsibleState.Expanded);
        item.contextValue = 'category';
        return item;
    }

    private static instructionItem(node: InstructionNode): vscode.TreeItem {
        const item = new vscode.TreeItem(node.entry.label, vscode.TreeItemCollapsibleState.None);
        item.contextValue = `instruction.${node.state}`;

        switch (node.state) {
            case InstructionState.Active:
                item.iconPath = new vscode.ThemeIcon('check', new vscode.ThemeColor('testing.iconPassed'));
                break;
            case InstructionState.NotDetected:
                item.iconPath = new vscode.ThemeIcon('circle-slash', new vscode.ThemeColor('disabledForeground'));
                item.description = 'not detected';
                break;
            case InstructionState.Disabled:
                item.iconPath = new vscode.ThemeIcon('x', new vscode.ThemeColor('testing.iconFailed'));
                item.description = 'disabled';
                break;
            case InstructionState.Overridden:
                item.iconPath = new vscode.ThemeIcon('file-symlink-file', new vscode.ThemeColor('terminal.ansiYellow'));
                item.description = 'overridden';
                break;
            default: {
                const _: never = node.state;
                void _;
                break;
            }
        }

        item.tooltip = InstructionsTreeProvider.tooltip(node);
        item.command = {
            command: 'vscode.open',
            title: 'Open Instruction',
            arguments: [vscode.Uri.from({ scheme: instructionScheme, path: node.entry.fileName })],
        };
        return item;
    }

    private static tooltip(node: InstructionNode): string {
        const lines = [node.entry.label];

        switch (node.state) {
            case InstructionState.Active:
                lines.push('Active — included in Copilot context');
                break;
            case InstructionState.NotDetected:
                lines.push('Not detected — workspace lacks matching files');
                break;
            case InstructionState.Disabled:
                lines.push('Disabled — turned off in settings');
                break;
            case InstructionState.Overridden:
                lines.push('Overridden — local .github/instructions file found');
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

    static async enableInstruction(node: InstructionNode): Promise<void> {
        await vscode.workspace.getConfiguration().update(node.entry.settingId, true, vscode.ConfigurationTarget.Global);
    }

    static async disableInstruction(node: InstructionNode): Promise<void> {
        await vscode.workspace.getConfiguration().update(node.entry.settingId, false, vscode.ConfigurationTarget.Global);
    }

    dispose(): void {
        for (const d of this.disposables) {
            d.dispose();
        }
    }
}
