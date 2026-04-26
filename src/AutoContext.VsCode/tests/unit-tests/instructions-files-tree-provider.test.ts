import { describe, it, expect, vi, beforeEach } from 'vitest';
import { TreeItemCollapsibleState, TreeItemCheckboxState, workspace, commands, Uri, window } from './_fakes/fake-vscode';
import { InstructionsFilesTreeProvider } from '../../src/instructions-files-tree-provider';
import type { AutoContextConfig } from '../../src/types/autocontext-config';
import type { InstructionsTreeNode } from '../../src/types/instructions-tree-node';
import { TreeViewNodeState } from '../../src/tree-view-node-state';
import { contextKeys } from '../../src/ui-constants';
import { InstructionsFilesManifestLoader } from '../../src/instructions-files-manifest-loader';
import { join } from 'node:path';
import { TreeViewStateResolver } from '../../src/tree-view-state-resolver';
import { TreeViewTooltip } from '../../src/tree-view-tooltip';
import { createFakeDetector, createFakeConfigManager, createFakeOutputChannel } from './_fakes';

const fakeDetector = createFakeDetector();
const outputChannel = createFakeOutputChannel();

const stateResolver = new TreeViewStateResolver(fakeDetector);
const tooltip = new TreeViewTooltip('instructions');

let currentConfig: AutoContextConfig = {};
const fakeConfigManager = createFakeConfigManager();

beforeEach(() => {
    vi.clearAllMocks();
    currentConfig = {};
    vi.mocked(fakeConfigManager.readSync).mockImplementation(() => currentConfig);
    vi.mocked(fakeConfigManager.onDidChange).mockReturnValue({ dispose: vi.fn() });
    vi.mocked(fakeDetector.get).mockReset();
    vi.mocked(fakeDetector.getOverriddenContextKeys).mockReturnValue(new Set());
    vi.mocked(fakeDetector.getOverrideVersion).mockReturnValue(undefined);
});

describe('InstructionsFilesTreeProvider', () => {
    const catalog = new InstructionsFilesManifestLoader(join(__dirname, '..', '..')).load();

    it('should return category nodes as root elements', () => {
        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();

        const names = roots.map(r => r.kind === 'categoryNode' ? r.name : '');
        expect.soft(names).toEqual(['General', 'Languages', '.NET', 'Web', 'Tools']);

        provider.dispose();
    });

    it('should return instruction nodes as children of a category', () => {
        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const general = roots.find(r => r.kind === 'categoryNode' && r.name === 'General')!;
        const children = provider.getChildren(general);

        expect.soft(children.length).toBeGreaterThan(0);
        expect.soft(children.every(c => c.kind === 'instructions')).toBe(true);

        provider.dispose();
    });

    it('should return no children for an instruction node', () => {
        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const general = roots.find(r => r.kind === 'categoryNode' && r.name === 'General')!;
        const children = provider.getChildren(general);
        const leaf = children[0];

        expect.soft(provider.getChildren(leaf)).toEqual([]);

        provider.dispose();
    });

    it('should show category items as expanded', () => {
        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const treeItem = provider.getTreeItem(roots[0]);

        expect.soft(treeItem.collapsibleState).toBe(TreeItemCollapsibleState.Expanded);

        provider.dispose();
    });

    it('should mark instructions as active when config is enabled and context keys match', () => {
        vi.mocked(fakeDetector.get).mockImplementation((key: string) => key === 'hasCSharp');

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'categoryNode' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const csharp = children.find(c => c.kind === 'instructions' && c.entry.runtimeInfo.contextKey === 'autocontext.instructions.lang-csharp')!;

        expect.soft(csharp.kind === 'instructions' && csharp.state).toBe(TreeViewNodeState.Enabled);

        provider.dispose();
    });

    it('should mark instructions as not detected when context keys do not match', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'categoryNode' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const csharp = children.find(c => c.kind === 'instructions' && c.entry.runtimeInfo.contextKey === 'autocontext.instructions.lang-csharp')!;

        expect.soft(csharp.kind === 'instructions' && csharp.state).toBe(TreeViewNodeState.NotDetected);

        const treeItem = provider.getTreeItem(csharp);
        expect.soft(treeItem.description).toBe('not detected');

        provider.dispose();
    });

    it('should mark instructions as disabled when config setting is false', () => {
        vi.mocked(fakeDetector.get).mockImplementation((key: string) => key === 'hasCSharp');
        currentConfig = { instructions: { 'lang-csharp.instructions.md': { enabled: false } } };

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'categoryNode' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const csharp = children.find(c => c.kind === 'instructions' && c.entry.runtimeInfo.contextKey === 'autocontext.instructions.lang-csharp')!;

        expect.soft(csharp.kind === 'instructions' && csharp.state).toBe(TreeViewNodeState.Disabled);

        const treeItem = provider.getTreeItem(csharp);
        expect.soft(treeItem.description).toBe('disabled');

        provider.dispose();
    });

    it('should mark instructions as overridden when contextKey is in overrides', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        vi.mocked(fakeDetector.getOverriddenContextKeys).mockReturnValue(new Set(['autocontext.instructions.lang-csharp']));

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'categoryNode' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const csharp = children.find(c => c.kind === 'instructions' && c.entry.runtimeInfo.contextKey === 'autocontext.instructions.lang-csharp')!;

        expect.soft(csharp.kind === 'instructions' && csharp.state).toBe(TreeViewNodeState.Overridden);

        const treeItem = provider.getTreeItem(csharp);
        expect.soft(treeItem.description).toBe('overridden');

        provider.dispose();
    });

    it('should treat always-on entries (no contextKeys) as active when enabled', () => {
        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const general = roots.find(r => r.kind === 'categoryNode' && r.name === 'General')!;
        const children = provider.getChildren(general);
        const codeReview = children.find(c => c.kind === 'instructions' && c.entry.runtimeInfo.contextKey === 'autocontext.instructions.code-review')!;

        expect.soft(codeReview.kind === 'instructions' && codeReview.state).toBe(TreeViewNodeState.Enabled);

        provider.dispose();
    });

    it('should preserve category order from the registry', () => {
        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();

        const categories = roots.filter(r => r.kind === 'categoryNode').map(r => r.kind === 'categoryNode' ? r.name : '');
        expect.soft(categories).toEqual(['General', 'Languages', '.NET', 'Web', 'Tools']);

        provider.dispose();
    });

    it('should sort instructions within a category by label (matching registry order)', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const general = roots.find(r => r.kind === 'categoryNode' && r.name === 'General')!;
        const children = provider.getChildren(general);
        const labels = children.map(c => c.kind === 'instructions' ? c.entry.label : '');
        const expected = catalog.instructions.filter(e => e.firstCategory.name === 'General').map(e => e.label);

        expect.soft(labels).toEqual(expected);

        provider.dispose();
    });

    it('should include tooltip with setting ID', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'categoryNode' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const csharp = children.find(c => c.kind === 'instructions' && c.entry.runtimeInfo.contextKey === 'autocontext.instructions.lang-csharp')!;

        const treeItem = provider.getTreeItem(csharp);
        expect.soft(treeItem.tooltip).toContain('autocontext.instructions.lang-csharp');

        provider.dispose();
    });

    it('should include description and version in tooltip when metadata is provided', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const metadata = new Map([
            ['lang-csharp.instructions.md', { description: 'C# coding guidelines', version: '1.0.0' }],
        ]);
        const enrichedCatalog = new InstructionsFilesManifestLoader(join(__dirname, '..', '..')).load(metadata);
        const provider = new InstructionsFilesTreeProvider(fakeDetector, enrichedCatalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'categoryNode' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const csharp = children.find(c => c.kind === 'instructions' && c.entry.runtimeInfo.contextKey === 'autocontext.instructions.lang-csharp')!;
        const treeItem = provider.getTreeItem(csharp);

        expect.soft(treeItem.tooltip).toContain('C# coding guidelines');
        expect.soft(treeItem.tooltip).toContain('v1.0.0');

        provider.dispose();
    });

    it('should not include version in tooltip when metadata is absent', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const general = roots.find(r => r.kind === 'categoryNode' && r.name === 'General')!;
        const children = provider.getChildren(general);
        const active = children.find(c => c.kind === 'instructions' && c.state === TreeViewNodeState.Enabled)!;
        const treeItem = provider.getTreeItem(active);

        expect.soft(treeItem.tooltip).not.toMatch(/\bv\d/);

        provider.dispose();
    });

    it('should sort active instructions before disabled, and disabled before not detected', () => {
        vi.mocked(fakeDetector.get).mockImplementation((key: string) => key === 'hasCSharp' || key === 'hasTypeScript');
        currentConfig = { instructions: { 'lang-csharp.instructions.md': { enabled: false } } };

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'categoryNode' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);

        const states = children
            .filter(c => c.kind === 'instructions')
            .map(c => c.kind === 'instructions' ? c.state : '');

        const firstDisabledIndex = states.indexOf(TreeViewNodeState.Disabled);
        const firstNotDetectedIndex = states.indexOf(TreeViewNodeState.NotDetected);
        const lastActiveIndex = states.lastIndexOf(TreeViewNodeState.Enabled);

        if (lastActiveIndex !== -1 && firstDisabledIndex !== -1) {
            expect.soft(lastActiveIndex).toBeLessThan(firstDisabledIndex);
        }
        if (firstDisabledIndex !== -1 && firstNotDetectedIndex !== -1) {
            expect.soft(firstDisabledIndex).toBeLessThan(firstNotDetectedIndex);
        }

        provider.dispose();
    });

    it('should set a command on instruction items to open the virtual document', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'categoryNode' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const csharp = children.find(c => c.kind === 'instructions' && c.entry.runtimeInfo.contextKey === 'autocontext.instructions.lang-csharp')!;

        const treeItem = provider.getTreeItem(csharp);
        expect.soft(treeItem.command).toBeDefined();
        expect.soft(treeItem.command!.command).toBe('vscode.open');
        expect.soft(treeItem.command!.arguments![0].scheme).toBe('autocontext-instructions');
        expect.soft(treeItem.command!.arguments![0].path).toBe(csharp.kind === 'instructions' ? csharp.entry.name : '');

        provider.dispose();
    });

    it('should open the workspace override file for overridden items', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        vi.mocked(fakeDetector.getOverriddenContextKeys).mockReturnValue(new Set(['autocontext.instructions.lang-csharp']));
        workspace.workspaceFolders = [{ uri: { path: '/workspace', scheme: 'file' } }];

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'categoryNode' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const csharp = children.find(c => c.kind === 'instructions' && c.entry.runtimeInfo.contextKey === 'autocontext.instructions.lang-csharp')!;

        const treeItem = provider.getTreeItem(csharp);
        expect.soft(treeItem.command).toBeDefined();
        expect.soft(treeItem.command!.command).toBe('vscode.open');
        expect.soft(treeItem.command!.arguments![0].scheme).toBe('file');
        expect.soft(treeItem.command!.arguments![0].path).toContain('.github/instructions/lang-csharp.instructions.md');

        provider.dispose();
    });

    it('should set contextValue to instruction.enabled for active items', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const general = roots.find(r => r.kind === 'categoryNode' && r.name === 'General')!;
        const children = provider.getChildren(general);
        const active = children.find(c => c.kind === 'instructions' && c.state === TreeViewNodeState.Enabled)!;

        const treeItem = provider.getTreeItem(active);
        expect.soft(treeItem.contextValue).toBe('instruction.enabled');

        provider.dispose();
    });

    it('should set contextValue to instruction.disabled for disabled items', () => {
        vi.mocked(fakeDetector.get).mockImplementation((key: string) => key === 'hasCSharp');
        currentConfig = { instructions: { 'lang-csharp.instructions.md': { enabled: false } } };

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'categoryNode' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const disabled = children.find(c => c.kind === 'instructions' && c.entry.runtimeInfo.contextKey === 'autocontext.instructions.lang-csharp')!;

        const treeItem = provider.getTreeItem(disabled);
        expect.soft(treeItem.contextValue).toBe('instruction.disabled');

        provider.dispose();
    });

    it('should append .hasChangelog to contextValue when entry has a changelog', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const metadata = new Map([
            ['lang-csharp.instructions.md', { description: 'C#', version: '1.0.0', hasChangelog: true }],
        ]);
        const changelogCatalog = new InstructionsFilesManifestLoader(join(__dirname, '..', '..')).load(metadata);
        const provider = new InstructionsFilesTreeProvider(fakeDetector, changelogCatalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'categoryNode' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const csharp = children.find(c => c.kind === 'instructions' && c.entry.runtimeInfo.contextKey === 'autocontext.instructions.lang-csharp')!;

        const treeItem = provider.getTreeItem(csharp);
        expect.soft(treeItem.contextValue).toBe('instruction.enabled.hasChangelog');

        provider.dispose();
    });

    it('should not append .hasChangelog when entry has no changelog', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const general = roots.find(r => r.kind === 'categoryNode' && r.name === 'General')!;
        const children = provider.getChildren(general);
        const active = children.find(c => c.kind === 'instructions' && c.state === TreeViewNodeState.Enabled)!;

        const treeItem = provider.getTreeItem(active);
        expect.soft(treeItem.contextValue).not.toContain('hasChangelog');

        provider.dispose();
    });

    it('should update setting to true when enableInstruction is called', async () => {
        vi.mocked(fakeDetector.get).mockImplementation((key: string) => key === 'hasCSharp');
        currentConfig = { instructions: { 'lang-csharp.instructions.md': { enabled: false } } };

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'categoryNode' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const node = children.find(c => c.kind === 'instructions' && c.entry.runtimeInfo.contextKey === 'autocontext.instructions.lang-csharp')!;

        await provider.enableInstruction(node as InstructionsTreeNode);

        expect.soft(vi.mocked(fakeConfigManager.setInstructionEnabled)).toHaveBeenCalledWith('lang-csharp.instructions.md', true);

        provider.dispose();
    });

    it('should update setting to false when disableInstruction is called', async () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const general = roots.find(r => r.kind === 'categoryNode' && r.name === 'General')!;
        const children = provider.getChildren(general);
        const node = children.find(c => c.kind === 'instructions' && c.state === TreeViewNodeState.Enabled)!;

        await provider.disableInstruction(node as InstructionsTreeNode);

        expect.soft(vi.mocked(fakeConfigManager.setInstructionEnabled)).toHaveBeenCalledWith(
            node.kind === 'instructions' ? node.entry.name : '',
            false,
        );

        provider.dispose();
    });

    it('should delete the override file and close its tab when deleteOverride is called', async () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        vi.mocked(fakeDetector.getOverriddenContextKeys).mockReturnValue(new Set(['autocontext.instructions.lang-csharp']));
        workspace.workspaceFolders = [{ uri: { path: '/workspace', scheme: 'file' } }];

        const folder = workspace.workspaceFolders[0] as { uri: { path: string; scheme: string } };
        const targetUri = Uri.joinPath(folder.uri, '.github/instructions/lang-csharp.instructions.md');
        const matchingTab = { input: { uri: { toString: () => targetUri.toString() } } };
        window.tabGroups.all = [{ tabs: [matchingTab] }];

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'categoryNode' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const node = children.find(c => c.kind === 'instructions' && c.entry.runtimeInfo.contextKey === 'autocontext.instructions.lang-csharp')!;

        await InstructionsFilesTreeProvider.deleteOverride(node as InstructionsTreeNode);

        expect.soft(window.tabGroups.close).toHaveBeenCalledWith(matchingTab);
        expect.soft(workspace.fs.delete).toHaveBeenCalled();

        provider.dispose();
    });

    it('should open the virtual document when showOriginal is called on overridden item', async () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        vi.mocked(fakeDetector.getOverriddenContextKeys).mockReturnValue(new Set(['autocontext.instructions.lang-csharp']));

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'categoryNode' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const node = children.find(c => c.kind === 'instructions' && c.entry.runtimeInfo.contextKey === 'autocontext.instructions.lang-csharp')!;

        await InstructionsFilesTreeProvider.showOriginal(node as InstructionsTreeNode);

        expect.soft(commands.executeCommand).toHaveBeenCalledWith(
            'vscode.open',
            expect.objectContaining({ scheme: 'autocontext-instructions', path: 'lang-csharp.instructions.md' }),
        );

        provider.dispose();
    });

    it('should show checkboxes on active and disabled items in export mode', () => {
        vi.mocked(fakeDetector.get).mockImplementation((key: string) => key === 'hasCSharp');
        currentConfig = { instructions: { 'design-principles.instructions.md': { enabled: false } } };

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        provider.enterExportMode();

        const roots = provider.getChildren();
        const general = roots.find(r => r.kind === 'categoryNode' && r.name === 'General')!;
        const children = provider.getChildren(general);

        const active = children.find(c => c.kind === 'instructions' && c.state === TreeViewNodeState.Enabled)!;
        const disabled = children.find(c => c.kind === 'instructions' && c.state === TreeViewNodeState.Disabled)!;

        expect.soft(provider.getTreeItem(active).checkboxState).toBe(TreeItemCheckboxState.Unchecked);
        expect.soft(provider.getTreeItem(disabled).checkboxState).toBe(TreeItemCheckboxState.Unchecked);

        provider.dispose();
    });

    it('should not show checkboxes on not-detected or overridden items in export mode', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        provider.enterExportMode();

        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'categoryNode' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const notDetected = children.find(c => c.kind === 'instructions' && c.state === TreeViewNodeState.NotDetected)!;

        expect.soft(provider.getTreeItem(notDetected).checkboxState).toBeUndefined();

        provider.dispose();
    });

    it('should not show checkboxes when not in export mode', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const general = roots.find(r => r.kind === 'categoryNode' && r.name === 'General')!;
        const children = provider.getChildren(general);
        const active = children.find(c => c.kind === 'instructions' && c.state === TreeViewNodeState.Enabled)!;

        expect.soft(provider.getTreeItem(active).checkboxState).toBeUndefined();

        provider.dispose();
    });

    it('should set export mode context key when entering export mode', () => {
        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        provider.enterExportMode();

        expect.soft(commands.executeCommand).toHaveBeenCalledWith('setContext', contextKeys.ExportMode, true);

        provider.dispose();
    });

    it('should clear export mode context key when canceling export mode', () => {
        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        provider.enterExportMode();
        provider.cancelExportMode();

        expect.soft(commands.executeCommand).toHaveBeenCalledWith('setContext', contextKeys.ExportMode, false);

        provider.dispose();
    });

    it('should return checked entries from getCheckedEntries', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        provider.enterExportMode();

        const roots = provider.getChildren();
        const general = roots.find(r => r.kind === 'categoryNode' && r.name === 'General')!;
        const children = provider.getChildren(general);
        const entry = children.find(c => c.kind === 'instructions' && c.entry.runtimeInfo.contextKey === 'autocontext.instructions.code-review')!;

        // Simulate checkbox toggle by accessing the internal checked set
        // In production, this is driven by onDidChangeCheckboxState
        if (entry.kind === 'instructions') {
            (provider as unknown as { _checkedEntries: Set<string> })._checkedEntries.add(entry.entry.runtimeInfo.contextKey);
        }

        const checked = provider.getCheckedEntries();
        expect.soft(checked.length).toBe(1);
        expect.soft(checked[0].runtimeInfo.contextKey).toBe('autocontext.instructions.code-review');

        provider.dispose();
    });

    it('should clear checked entries when canceling export mode', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        provider.enterExportMode();

        (provider as unknown as { _checkedEntries: Set<string> })._checkedEntries.add('autocontext.instructions.code-review');
        expect.soft(provider.getCheckedEntries().length).toBe(1);

        provider.cancelExportMode();
        expect.soft(provider.getCheckedEntries().length).toBe(0);

        provider.dispose();
    });

    it('should set badge on tree view when setBadge is called with a positive value', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        provider.setBadge(1, 'New version available');

        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        expect.soft(treeView.badge).toEqual({ value: 1, tooltip: 'New version available' });

        provider.dispose();
    });

    it('should clear badge on tree view when setBadge is called with zero', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        provider.setBadge(1, 'New version available');
        provider.setBadge(0, '');

        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        expect.soft(treeView.badge).toBeUndefined();

        provider.dispose();
    });

    it('should clear badge when tree view becomes visible after dismissBadgeOnNextReveal', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        const onDismiss = vi.fn();

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        provider.setBadge(1, 'New version available');
        provider.dismissBadgeOnNextReveal(onDismiss);

        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        treeView.__fireVisibility(true);

        expect.soft(treeView.badge).toBeUndefined();
        expect.soft(onDismiss).toHaveBeenCalledOnce();

        provider.dispose();
    });

    it('should not clear badge when tree view becomes hidden', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        provider.setBadge(1, 'New version available');
        provider.dismissBadgeOnNextReveal();

        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        treeView.__fireVisibility(false);

        expect.soft(treeView.badge).toEqual({ value: 1, tooltip: 'New version available' });

        provider.dispose();
    });

    it('should only dismiss badge once after dismissBadgeOnNextReveal', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        const onDismiss = vi.fn();

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        provider.setBadge(1, 'New version available');
        provider.dismissBadgeOnNextReveal(onDismiss);

        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        treeView.__fireVisibility(true);

        // Set badge again manually — should not auto-clear
        provider.setBadge(2, 'Another update');
        treeView.__fireVisibility(true);

        expect.soft(treeView.badge).toEqual({ value: 2, tooltip: 'Another update' });
        expect.soft(onDismiss).toHaveBeenCalledOnce();

        provider.dispose();
    });

    it('should hide not-detected instructions when showNotDetected is false', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        provider.showNotDetected = false;

        const roots = provider.getChildren();
        // All items are not-detected except context-free ones → categories with only not-detected items are hidden
        for (const cat of roots) {
            if (cat.kind !== 'categoryNode') { continue; }
            const children = provider.getChildren(cat);
            const notDetected = children.filter(c => c.kind === 'instructions' && c.state === TreeViewNodeState.NotDetected);
            expect.soft(notDetected).toHaveLength(0);
        }

        provider.dispose();
    });

    it('should show not-detected instructions when showNotDetected is true', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        provider.showNotDetected = true;

        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'categoryNode' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const notDetected = children.filter(c => c.kind === 'instructions' && c.state === TreeViewNodeState.NotDetected);
        expect.soft(notDetected.length).toBeGreaterThan(0);

        provider.dispose();
    });

    it('should hide empty categories when showNotDetected is false', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        provider.showNotDetected = false;

        const roots = provider.getChildren();
        for (const cat of roots) {
            if (cat.kind !== 'categoryNode') { continue; }
            const children = provider.getChildren(cat);
            expect.soft(children.length).toBeGreaterThan(0);
        }

        provider.dispose();
    });

    it('should set contextValue to instruction.overridden for overridden items', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        vi.mocked(fakeDetector.getOverriddenContextKeys).mockReturnValue(new Set(['autocontext.instructions.lang-csharp']));

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'categoryNode' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const overridden = children.find(c => c.kind === 'instructions' && c.entry.runtimeInfo.contextKey === 'autocontext.instructions.lang-csharp')!;

        const treeItem = provider.getTreeItem(overridden);
        expect.soft(treeItem.contextValue).toBe('instruction.overridden');

        provider.dispose();
    });

    it('should include state description in tooltip for active state', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();

        const general = roots.find(r => r.kind === 'categoryNode' && r.name === 'General')!;
        const activeNode = provider.getChildren(general).find(c => c.kind === 'instructions' && c.state === TreeViewNodeState.Enabled)!;
        expect.soft(provider.getTreeItem(activeNode).tooltip).toContain('Active');

        provider.dispose();
    });

    it('should include state description in tooltip for not-detected state', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'categoryNode' && r.name === 'Languages')!;
        const notDetectedNode = provider.getChildren(languages).find(c => c.kind === 'instructions' && c.state === TreeViewNodeState.NotDetected)!;
        expect.soft(provider.getTreeItem(notDetectedNode).tooltip).toContain('Not detected');

        provider.dispose();
    });

    it('should include state description in tooltip for disabled state', () => {
        vi.mocked(fakeDetector.get).mockImplementation((key: string) => key === 'hasCSharp');
        currentConfig = { instructions: { 'lang-csharp.instructions.md': { enabled: false } } };

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'categoryNode' && r.name === 'Languages')!;
        const disabled = provider.getChildren(languages).find(c => c.kind === 'instructions' && c.state === TreeViewNodeState.Disabled)!;

        expect.soft(provider.getTreeItem(disabled).tooltip).toContain('Disabled');

        provider.dispose();
    });

    it('should include state description in tooltip for overridden state', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        vi.mocked(fakeDetector.getOverriddenContextKeys).mockReturnValue(new Set(['autocontext.instructions.lang-csharp']));

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'categoryNode' && r.name === 'Languages')!;
        const overridden = provider.getChildren(languages).find(c => c.kind === 'instructions' && c.state === TreeViewNodeState.Overridden)!;

        expect.soft(provider.getTreeItem(overridden).tooltip).toContain('Overridden');

        provider.dispose();
    });

    it('should show active/total count in category tooltip when all detected', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        currentConfig = { instructions: { 'lang-csharp.instructions.md': { enabled: false } } };

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'categoryNode' && r.name === 'Languages')!;
        const item = provider.getTreeItem(languages);
        const langEntries = catalog.instructions.filter(e => e.firstCategory.name === 'Languages');
        const active = langEntries.filter(e => e.runtimeInfo.contextKey !== 'autocontext.instructions.lang-csharp').length;

        expect.soft(item.tooltip).toBe(`Languages\n${active}/${langEntries.length} active`);

        provider.dispose();
    });

    it('should show active/total count in category tooltip with not-detected entries', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const general = roots.find(r => r.kind === 'categoryNode' && r.name === 'General')!;
        const item = provider.getTreeItem(general);
        const generalEntries = catalog.instructions.filter(e => e.firstCategory.name === 'General');
        const alwaysOn = generalEntries.filter(e => !e.activationFlags || e.activationFlags.length === 0).length;

        expect.soft(item.tooltip).toBe(`General\n${alwaysOn}/${generalEntries.length} active`);

        provider.dispose();
    });

    it('should count overridden instructions as active in category tooltip', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        vi.mocked(fakeDetector.getOverriddenContextKeys).mockReturnValue(new Set(['autocontext.instructions.lang-csharp']));

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'categoryNode' && r.name === 'Languages')!;
        const item = provider.getTreeItem(languages);
        const langEntries = catalog.instructions.filter(e => e.firstCategory.name === 'Languages');

        expect.soft(item.tooltip).toBe(`Languages\n${langEntries.length}/${langEntries.length} active`);

        provider.dispose();
    });

    it('should set treeView description to enabled/total count', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        currentConfig = { instructions: { 'lang-csharp.instructions.md': { enabled: false } } };

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        const total = catalog.count;
        const enabled = catalog.instructions.filter(e => e.runtimeInfo.contextKey !== 'autocontext.instructions.lang-csharp').length;

        expect.soft(treeView.description).toBe(`${enabled}/${total}`);

        provider.dispose();
    });

    it('should exclude not-detected entries from enabled count in description', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        const total = catalog.count;
        const alwaysOn = catalog.instructions.filter(e => !e.activationFlags || e.activationFlags.length === 0).length;

        expect.soft(treeView.description).toBe(`${alwaysOn}/${total}`);

        provider.dispose();
    });

    it('should show outdated description when override version is behind built-in', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        vi.mocked(fakeDetector.getOverriddenContextKeys).mockReturnValue(new Set(['autocontext.instructions.lang-csharp']));
        vi.mocked(fakeDetector.getOverrideVersion).mockImplementation(
            (fileName: string) => fileName === 'lang-csharp.instructions.md' ? '1.0.0' : undefined,
        );

        const metadata = new Map([['lang-csharp.instructions.md', { version: '1.1.0' }]]);
        const versionedCatalog = new InstructionsFilesManifestLoader(join(__dirname, '..', '..')).load(metadata);
        const provider = new InstructionsFilesTreeProvider(fakeDetector, versionedCatalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'categoryNode' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const csharp = children.find(c => c.kind === 'instructions' && c.entry.runtimeInfo.contextKey === 'autocontext.instructions.lang-csharp')!;

        const treeItem = provider.getTreeItem(csharp);
        expect.soft(treeItem.description).toBe('overridden (outdated)');
        expect.soft(treeItem.contextValue).toContain('.outdated');
        expect.soft(treeItem.tooltip).toContain('local file is outdated');

        provider.dispose();
    });

    it('should show standard overridden tooltip when override version matches built-in', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        vi.mocked(fakeDetector.getOverriddenContextKeys).mockReturnValue(new Set(['autocontext.instructions.lang-csharp']));
        vi.mocked(fakeDetector.getOverrideVersion).mockImplementation(
            (fileName: string) => fileName === 'lang-csharp.instructions.md' ? '1.0.0' : undefined,
        );

        const metadata = new Map([['lang-csharp.instructions.md', { version: '1.0.0' }]]);
        const versionedCatalog = new InstructionsFilesManifestLoader(join(__dirname, '..', '..')).load(metadata);
        const provider = new InstructionsFilesTreeProvider(fakeDetector, versionedCatalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'categoryNode' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const csharp = children.find(c => c.kind === 'instructions' && c.entry.runtimeInfo.contextKey === 'autocontext.instructions.lang-csharp')!;

        const treeItem = provider.getTreeItem(csharp);
        expect.soft(treeItem.description).toBe('overridden');
        expect.soft(treeItem.contextValue).not.toContain('.outdated');
        expect.soft(treeItem.tooltip).toContain('using a local file instead');

        provider.dispose();
    });

    it('should not show outdated when override has no version in frontmatter', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        vi.mocked(fakeDetector.getOverriddenContextKeys).mockReturnValue(new Set(['autocontext.instructions.lang-csharp']));
        vi.mocked(fakeDetector.getOverrideVersion).mockReturnValue(undefined);

        const metadata = new Map([['lang-csharp.instructions.md', { version: '1.1.0' }]]);
        const versionedCatalog = new InstructionsFilesManifestLoader(join(__dirname, '..', '..')).load(metadata);
        const provider = new InstructionsFilesTreeProvider(fakeDetector, versionedCatalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'categoryNode' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const csharp = children.find(c => c.kind === 'instructions' && c.entry.runtimeInfo.contextKey === 'autocontext.instructions.lang-csharp')!;

        const treeItem = provider.getTreeItem(csharp);
        expect.soft(treeItem.description).toBe('overridden');

        provider.dispose();
    });

    it('should show warning dialog when deleting an outdated override', async () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        vi.mocked(fakeDetector.getOverriddenContextKeys).mockReturnValue(new Set(['autocontext.instructions.lang-csharp']));
        vi.mocked(fakeDetector.getOverrideVersion).mockImplementation(
            (fileName: string) => fileName === 'lang-csharp.instructions.md' ? '1.0.0' : undefined,
        );
        workspace.workspaceFolders = [{ uri: { path: '/workspace', scheme: 'file' } }];
        vi.mocked(window.showWarningMessage).mockResolvedValue('Delete' as never);

        const metadata = new Map([['lang-csharp.instructions.md', { version: '1.1.0' }]]);
        const versionedCatalog = new InstructionsFilesManifestLoader(join(__dirname, '..', '..')).load(metadata);
        const provider = new InstructionsFilesTreeProvider(fakeDetector, versionedCatalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'categoryNode' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const node = children.find(c => c.kind === 'instructions' && c.entry.runtimeInfo.contextKey === 'autocontext.instructions.lang-csharp')!;

        await InstructionsFilesTreeProvider.deleteOverride(node as InstructionsTreeNode);

        expect.soft(window.showWarningMessage).toHaveBeenCalledWith(
            expect.stringContaining('(v1.0.0) is behind AutoContext\'s version (v1.1.0)'),
            { modal: true },
            'Delete',
        );
        expect.soft(workspace.fs.delete).toHaveBeenCalled();

        provider.dispose();
    });

    it('should not delete override when user cancels the outdated warning dialog', async () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        vi.mocked(fakeDetector.getOverriddenContextKeys).mockReturnValue(new Set(['autocontext.instructions.lang-csharp']));
        vi.mocked(fakeDetector.getOverrideVersion).mockImplementation(
            (fileName: string) => fileName === 'lang-csharp.instructions.md' ? '1.0.0' : undefined,
        );
        workspace.workspaceFolders = [{ uri: { path: '/workspace', scheme: 'file' } }];
        vi.mocked(window.showWarningMessage).mockResolvedValue(undefined as never);

        const metadata = new Map([['lang-csharp.instructions.md', { version: '1.1.0' }]]);
        const versionedCatalog = new InstructionsFilesManifestLoader(join(__dirname, '..', '..')).load(metadata);
        const provider = new InstructionsFilesTreeProvider(fakeDetector, versionedCatalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'categoryNode' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const node = children.find(c => c.kind === 'instructions' && c.entry.runtimeInfo.contextKey === 'autocontext.instructions.lang-csharp')!;

        await InstructionsFilesTreeProvider.deleteOverride(node as InstructionsTreeNode);

        expect.soft(workspace.fs.delete).not.toHaveBeenCalled();

        provider.dispose();
    });

    it('should skip warning dialog when deleting a non-outdated override', async () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        vi.mocked(fakeDetector.getOverriddenContextKeys).mockReturnValue(new Set(['autocontext.instructions.lang-csharp']));
        vi.mocked(fakeDetector.getOverrideVersion).mockReturnValue(undefined);
        workspace.workspaceFolders = [{ uri: { path: '/workspace', scheme: 'file' } }];

        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'categoryNode' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const node = children.find(c => c.kind === 'instructions' && c.entry.runtimeInfo.contextKey === 'autocontext.instructions.lang-csharp')!;

        await InstructionsFilesTreeProvider.deleteOverride(node as InstructionsTreeNode);

        expect.soft(window.showWarningMessage).not.toHaveBeenCalled();
        expect.soft(workspace.fs.delete).toHaveBeenCalled();

        provider.dispose();
    });

    it('should log to outputChannel when configManager.read rejects in onDidChange', async () => {
        let onDidChangeCallback!: () => void;
        const failingConfigManager = {
            readSync: vi.fn(() => ({})),
            read: vi.fn().mockRejectedValue(new Error('read boom')),
            onDidChange: vi.fn((cb: () => void) => { onDidChangeCallback = cb; return { dispose: vi.fn() }; }),
        } as unknown as import('../../src/autocontext-config').AutoContextConfigManager;

        const oc = createFakeOutputChannel();
        const provider = new InstructionsFilesTreeProvider(fakeDetector, catalog, stateResolver, tooltip, failingConfigManager, oc);

        onDidChangeCallback();
        await vi.waitFor(() => {
            expect(oc.appendLine).toHaveBeenCalledWith(
                expect.stringContaining('[InstructionsTree] Failed to update config: read boom'),
            );
        });

        provider.dispose();
    });
});
