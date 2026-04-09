import { describe, it, expect, vi, beforeEach } from 'vitest';
import { __setConfigStore, TreeItemCollapsibleState, TreeItemCheckboxState, workspace, ConfigurationTarget, commands, Uri, window } from './__mocks__/vscode';
import { InstructionsTreeProvider } from '../../src/instructions-tree-provider';
import type { InstructionsTreeNode } from '../../src/types/instructions-tree-node';
import { InstructionsState } from '../../src/ui-constants';
import { InstructionsCatalog } from '../../src/instructions-catalog';
import { instructionsFiles, contextKeys } from '../../src/ui-constants';

const fakeDetector = {
    get: vi.fn((_key: string) => false),
    getOverriddenSettingIds: vi.fn(() => new Set<string>()),
    onDidDetect: vi.fn(() => ({ dispose: vi.fn() })),
} as unknown as import('../../src/workspace-context-detector').WorkspaceContextDetector;

beforeEach(() => {
    vi.clearAllMocks();
    __setConfigStore({});
    vi.mocked(fakeDetector.get).mockReset();
    vi.mocked(fakeDetector.getOverriddenSettingIds).mockReturnValue(new Set());
});

describe('InstructionsTreeProvider', () => {
    const catalog = new InstructionsCatalog(instructionsFiles);

    it('should return category nodes as root elements', () => {
        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const roots = provider.getChildren();

        const names = roots.map(r => r.kind === 'category' ? r.name : '');
        expect.soft(names).toEqual(['General', 'Languages', '.NET', 'Web', 'Tools']);

        provider.dispose();
    });

    it('should return instruction nodes as children of a category', () => {
        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const roots = provider.getChildren();
        const general = roots.find(r => r.kind === 'category' && r.name === 'General')!;
        const children = provider.getChildren(general);

        expect.soft(children.length).toBeGreaterThan(0);
        expect.soft(children.every(c => c.kind === 'instructions')).toBe(true);

        provider.dispose();
    });

    it('should return no children for an instruction node', () => {
        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const roots = provider.getChildren();
        const general = roots.find(r => r.kind === 'category' && r.name === 'General')!;
        const children = provider.getChildren(general);
        const leaf = children[0];

        expect.soft(provider.getChildren(leaf)).toEqual([]);

        provider.dispose();
    });

    it('should show category items as expanded', () => {
        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const roots = provider.getChildren();
        const treeItem = provider.getTreeItem(roots[0]);

        expect.soft(treeItem.collapsibleState).toBe(TreeItemCollapsibleState.Expanded);

        provider.dispose();
    });

    it('should mark instructions as active when config is enabled and context keys match', () => {
        vi.mocked(fakeDetector.get).mockImplementation((key: string) => key === 'hasCSharp');

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'category' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const csharp = children.find(c => c.kind === 'instructions' && c.entry.settingId === 'sharppilot.instructions.lang.csharp')!;

        expect.soft(csharp.kind === 'instructions' && csharp.state).toBe(InstructionsState.Active);

        provider.dispose();
    });

    it('should mark instructions as not detected when context keys do not match', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'category' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const csharp = children.find(c => c.kind === 'instructions' && c.entry.settingId === 'sharppilot.instructions.lang.csharp')!;

        expect.soft(csharp.kind === 'instructions' && csharp.state).toBe(InstructionsState.NotDetected);

        const treeItem = provider.getTreeItem(csharp);
        expect.soft(treeItem.description).toBe('not detected');

        provider.dispose();
    });

    it('should mark instructions as disabled when config setting is false', () => {
        vi.mocked(fakeDetector.get).mockImplementation((key: string) => key === 'hasCSharp');
        __setConfigStore({ 'sharppilot.instructions.lang.csharp': false });

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'category' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const csharp = children.find(c => c.kind === 'instructions' && c.entry.settingId === 'sharppilot.instructions.lang.csharp')!;

        expect.soft(csharp.kind === 'instructions' && csharp.state).toBe(InstructionsState.Disabled);

        const treeItem = provider.getTreeItem(csharp);
        expect.soft(treeItem.description).toBe('disabled');

        provider.dispose();
    });

    it('should mark instructions as overridden when settingId is in overrides', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        vi.mocked(fakeDetector.getOverriddenSettingIds).mockReturnValue(new Set(['sharppilot.instructions.lang.csharp']));

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'category' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const csharp = children.find(c => c.kind === 'instructions' && c.entry.settingId === 'sharppilot.instructions.lang.csharp')!;

        expect.soft(csharp.kind === 'instructions' && csharp.state).toBe(InstructionsState.Overridden);

        const treeItem = provider.getTreeItem(csharp);
        expect.soft(treeItem.description).toBe('overridden');

        provider.dispose();
    });

    it('should treat always-on entries (no contextKeys) as active when enabled', () => {
        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const roots = provider.getChildren();
        const general = roots.find(r => r.kind === 'category' && r.name === 'General')!;
        const children = provider.getChildren(general);
        const codeReview = children.find(c => c.kind === 'instructions' && c.entry.settingId === 'sharppilot.instructions.codeReview')!;

        expect.soft(codeReview.kind === 'instructions' && codeReview.state).toBe(InstructionsState.Active);

        provider.dispose();
    });

    it('should preserve category order from the registry', () => {
        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const roots = provider.getChildren();

        const categories = roots.filter(r => r.kind === 'category').map(r => r.kind === 'category' ? r.name : '');
        expect.soft(categories).toEqual(['General', 'Languages', '.NET', 'Web', 'Tools']);

        provider.dispose();
    });

    it('should sort instructions within a category by label (matching registry order)', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const roots = provider.getChildren();
        const general = roots.find(r => r.kind === 'category' && r.name === 'General')!;
        const children = provider.getChildren(general);
        const labels = children.map(c => c.kind === 'instructions' ? c.entry.label : '');
        const expected = catalog.all.filter(e => e.category === 'General').map(e => e.label);

        expect.soft(labels).toEqual(expected);

        provider.dispose();
    });

    it('should include tooltip with setting ID', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'category' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const csharp = children.find(c => c.kind === 'instructions' && c.entry.settingId === 'sharppilot.instructions.lang.csharp')!;

        const treeItem = provider.getTreeItem(csharp);
        expect.soft(treeItem.tooltip).toContain('sharppilot.instructions.lang.csharp');

        provider.dispose();
    });

    it('should sort active instructions before disabled, and disabled before not detected', () => {
        vi.mocked(fakeDetector.get).mockImplementation((key: string) => key === 'hasCSharp' || key === 'hasTypeScript');
        __setConfigStore({ 'sharppilot.instructions.lang.csharp': false });

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'category' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);

        const states = children
            .filter(c => c.kind === 'instructions')
            .map(c => c.kind === 'instructions' ? c.state : '');

        const firstDisabledIndex = states.indexOf(InstructionsState.Disabled);
        const firstNotDetectedIndex = states.indexOf(InstructionsState.NotDetected);
        const lastActiveIndex = states.lastIndexOf(InstructionsState.Active);

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

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'category' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const csharp = children.find(c => c.kind === 'instructions' && c.entry.settingId === 'sharppilot.instructions.lang.csharp')!;

        const treeItem = provider.getTreeItem(csharp);
        expect.soft(treeItem.command).toBeDefined();
        expect.soft(treeItem.command!.command).toBe('vscode.open');
        expect.soft(treeItem.command!.arguments![0].scheme).toBe('sharppilot-instructions');
        expect.soft(treeItem.command!.arguments![0].path).toBe(csharp.kind === 'instructions' ? csharp.entry.fileName : '');

        provider.dispose();
    });

    it('should open the workspace override file for overridden items', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        vi.mocked(fakeDetector.getOverriddenSettingIds).mockReturnValue(new Set(['sharppilot.instructions.lang.csharp']));
        workspace.workspaceFolders = [{ uri: { path: '/workspace', scheme: 'file' } }];

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'category' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const csharp = children.find(c => c.kind === 'instructions' && c.entry.settingId === 'sharppilot.instructions.lang.csharp')!;

        const treeItem = provider.getTreeItem(csharp);
        expect.soft(treeItem.command).toBeDefined();
        expect.soft(treeItem.command!.command).toBe('vscode.open');
        expect.soft(treeItem.command!.arguments![0].scheme).toBe('file');
        expect.soft(treeItem.command!.arguments![0].path).toContain('.github/instructions/lang-csharp.instructions.md');

        provider.dispose();
    });

    it('should set contextValue to instruction.active for active items', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const roots = provider.getChildren();
        const general = roots.find(r => r.kind === 'category' && r.name === 'General')!;
        const children = provider.getChildren(general);
        const active = children.find(c => c.kind === 'instructions' && c.state === InstructionsState.Active)!;

        const treeItem = provider.getTreeItem(active);
        expect.soft(treeItem.contextValue).toBe('instruction.active');

        provider.dispose();
    });

    it('should set contextValue to instruction.disabled for disabled items', () => {
        vi.mocked(fakeDetector.get).mockImplementation((key: string) => key === 'hasCSharp');
        __setConfigStore({ 'sharppilot.instructions.lang.csharp': false });

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'category' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const disabled = children.find(c => c.kind === 'instructions' && c.entry.settingId === 'sharppilot.instructions.lang.csharp')!;

        const treeItem = provider.getTreeItem(disabled);
        expect.soft(treeItem.contextValue).toBe('instruction.disabled');

        provider.dispose();
    });

    it('should update setting to true when enableInstruction is called', async () => {
        vi.mocked(fakeDetector.get).mockImplementation((key: string) => key === 'hasCSharp');
        __setConfigStore({ 'sharppilot.instructions.lang.csharp': false });

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'category' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const node = children.find(c => c.kind === 'instructions' && c.entry.settingId === 'sharppilot.instructions.lang.csharp')!;

        await InstructionsTreeProvider.enableInstruction(node as InstructionsTreeNode);

        const config = vi.mocked(workspace.getConfiguration).mock.results.at(-1)!.value;
        expect.soft(config.update).toHaveBeenCalledWith('sharppilot.instructions.lang.csharp', true, ConfigurationTarget.Global);

        provider.dispose();
    });

    it('should update setting to false when disableInstruction is called', async () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const roots = provider.getChildren();
        const general = roots.find(r => r.kind === 'category' && r.name === 'General')!;
        const children = provider.getChildren(general);
        const node = children.find(c => c.kind === 'instructions' && c.state === InstructionsState.Active)!;

        await InstructionsTreeProvider.disableInstruction(node as InstructionsTreeNode);

        const config = vi.mocked(workspace.getConfiguration).mock.results.at(-1)!.value;
        expect.soft(config.update).toHaveBeenCalledWith(
            node.kind === 'instructions' ? node.entry.settingId : '',
            false,
            ConfigurationTarget.Global,
        );

        provider.dispose();
    });

    it('should delete the override file and close its tab when deleteOverride is called', async () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        vi.mocked(fakeDetector.getOverriddenSettingIds).mockReturnValue(new Set(['sharppilot.instructions.lang.csharp']));
        workspace.workspaceFolders = [{ uri: { path: '/workspace', scheme: 'file' } }];

        const folder = workspace.workspaceFolders[0] as { uri: { path: string; scheme: string } };
        const targetUri = Uri.joinPath(folder.uri, '.github/instructions/lang-csharp.instructions.md');
        const matchingTab = { input: { uri: { toString: () => targetUri.toString() } } };
        window.tabGroups.all = [{ tabs: [matchingTab] }];

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'category' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const node = children.find(c => c.kind === 'instructions' && c.entry.settingId === 'sharppilot.instructions.lang.csharp')!;

        await InstructionsTreeProvider.deleteOverride(node as InstructionsTreeNode);

        expect.soft(window.tabGroups.close).toHaveBeenCalledWith(matchingTab);
        expect.soft(workspace.fs.delete).toHaveBeenCalled();

        provider.dispose();
    });

    it('should open the virtual document when showOriginal is called on overridden item', async () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        vi.mocked(fakeDetector.getOverriddenSettingIds).mockReturnValue(new Set(['sharppilot.instructions.lang.csharp']));

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'category' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const node = children.find(c => c.kind === 'instructions' && c.entry.settingId === 'sharppilot.instructions.lang.csharp')!;

        await InstructionsTreeProvider.showOriginal(node as InstructionsTreeNode);

        expect.soft(commands.executeCommand).toHaveBeenCalledWith(
            'vscode.open',
            expect.objectContaining({ scheme: 'sharppilot-instructions', path: 'lang-csharp.instructions.md' }),
        );

        provider.dispose();
    });

    it('should show checkboxes on active and disabled items in export mode', () => {
        vi.mocked(fakeDetector.get).mockImplementation((key: string) => key === 'hasCSharp');
        __setConfigStore({ 'sharppilot.instructions.designPrinciples': false });

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        provider.enterExportMode();

        const roots = provider.getChildren();
        const general = roots.find(r => r.kind === 'category' && r.name === 'General')!;
        const children = provider.getChildren(general);

        const active = children.find(c => c.kind === 'instructions' && c.state === InstructionsState.Active)!;
        const disabled = children.find(c => c.kind === 'instructions' && c.state === InstructionsState.Disabled)!;

        expect.soft(provider.getTreeItem(active).checkboxState).toBe(TreeItemCheckboxState.Unchecked);
        expect.soft(provider.getTreeItem(disabled).checkboxState).toBe(TreeItemCheckboxState.Unchecked);

        provider.dispose();
    });

    it('should not show checkboxes on not-detected or overridden items in export mode', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        provider.enterExportMode();

        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'category' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const notDetected = children.find(c => c.kind === 'instructions' && c.state === InstructionsState.NotDetected)!;

        expect.soft(provider.getTreeItem(notDetected).checkboxState).toBeUndefined();

        provider.dispose();
    });

    it('should not show checkboxes when not in export mode', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const roots = provider.getChildren();
        const general = roots.find(r => r.kind === 'category' && r.name === 'General')!;
        const children = provider.getChildren(general);
        const active = children.find(c => c.kind === 'instructions' && c.state === InstructionsState.Active)!;

        expect.soft(provider.getTreeItem(active).checkboxState).toBeUndefined();

        provider.dispose();
    });

    it('should set export mode context key when entering export mode', () => {
        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        provider.enterExportMode();

        expect.soft(commands.executeCommand).toHaveBeenCalledWith('setContext', contextKeys.ExportMode, true);

        provider.dispose();
    });

    it('should clear export mode context key when canceling export mode', () => {
        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        provider.enterExportMode();
        provider.cancelExportMode();

        expect.soft(commands.executeCommand).toHaveBeenCalledWith('setContext', contextKeys.ExportMode, false);

        provider.dispose();
    });

    it('should return checked entries from getCheckedEntries', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        provider.enterExportMode();

        const roots = provider.getChildren();
        const general = roots.find(r => r.kind === 'category' && r.name === 'General')!;
        const children = provider.getChildren(general);
        const entry = children.find(c => c.kind === 'instructions' && c.entry.settingId === 'sharppilot.instructions.codeReview')!;

        // Simulate checkbox toggle by accessing the internal checked set
        // In production, this is driven by onDidChangeCheckboxState
        if (entry.kind === 'instructions') {
            (provider as unknown as { _checkedEntries: Set<string> })._checkedEntries.add(entry.entry.settingId);
        }

        const checked = provider.getCheckedEntries();
        expect.soft(checked.length).toBe(1);
        expect.soft(checked[0].settingId).toBe('sharppilot.instructions.codeReview');

        provider.dispose();
    });

    it('should clear checked entries when canceling export mode', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        provider.enterExportMode();

        (provider as unknown as { _checkedEntries: Set<string> })._checkedEntries.add('sharppilot.instructions.codeReview');
        expect.soft(provider.getCheckedEntries().length).toBe(1);

        provider.cancelExportMode();
        expect.soft(provider.getCheckedEntries().length).toBe(0);

        provider.dispose();
    });

    it('should hide not-detected instructions when showNotDetected is false', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        provider.showNotDetected = false;

        const roots = provider.getChildren();
        // All items are not-detected except context-free ones → categories with only not-detected items are hidden
        for (const cat of roots) {
            if (cat.kind !== 'category') { continue; }
            const children = provider.getChildren(cat);
            const notDetected = children.filter(c => c.kind === 'instructions' && c.state === InstructionsState.NotDetected);
            expect.soft(notDetected).toHaveLength(0);
        }

        provider.dispose();
    });

    it('should show not-detected instructions when showNotDetected is true', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        provider.showNotDetected = true;

        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'category' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const notDetected = children.filter(c => c.kind === 'instructions' && c.state === InstructionsState.NotDetected);
        expect.soft(notDetected.length).toBeGreaterThan(0);

        provider.dispose();
    });

    it('should hide empty categories when showNotDetected is false', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        provider.showNotDetected = false;

        const roots = provider.getChildren();
        for (const cat of roots) {
            if (cat.kind !== 'category') { continue; }
            const children = provider.getChildren(cat);
            expect.soft(children.length).toBeGreaterThan(0);
        }

        provider.dispose();
    });

    it('should set contextValue to instruction.overridden for overridden items', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        vi.mocked(fakeDetector.getOverriddenSettingIds).mockReturnValue(new Set(['sharppilot.instructions.lang.csharp']));

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'category' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const overridden = children.find(c => c.kind === 'instructions' && c.entry.settingId === 'sharppilot.instructions.lang.csharp')!;

        const treeItem = provider.getTreeItem(overridden);
        expect.soft(treeItem.contextValue).toBe('instruction.overridden');

        provider.dispose();
    });

    it('should include state description in tooltip for active state', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const roots = provider.getChildren();

        const general = roots.find(r => r.kind === 'category' && r.name === 'General')!;
        const activeNode = provider.getChildren(general).find(c => c.kind === 'instructions' && c.state === InstructionsState.Active)!;
        expect.soft(provider.getTreeItem(activeNode).tooltip).toContain('Active');

        provider.dispose();
    });

    it('should include state description in tooltip for not-detected state', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'category' && r.name === 'Languages')!;
        const notDetectedNode = provider.getChildren(languages).find(c => c.kind === 'instructions' && c.state === InstructionsState.NotDetected)!;
        expect.soft(provider.getTreeItem(notDetectedNode).tooltip).toContain('Not detected');

        provider.dispose();
    });

    it('should include state description in tooltip for disabled state', () => {
        vi.mocked(fakeDetector.get).mockImplementation((key: string) => key === 'hasCSharp');
        __setConfigStore({ 'sharppilot.instructions.lang.csharp': false });

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'category' && r.name === 'Languages')!;
        const disabled = provider.getChildren(languages).find(c => c.kind === 'instructions' && c.state === InstructionsState.Disabled)!;

        expect.soft(provider.getTreeItem(disabled).tooltip).toContain('Disabled');

        provider.dispose();
    });

    it('should include state description in tooltip for overridden state', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        vi.mocked(fakeDetector.getOverriddenSettingIds).mockReturnValue(new Set(['sharppilot.instructions.lang.csharp']));

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'category' && r.name === 'Languages')!;
        const overridden = provider.getChildren(languages).find(c => c.kind === 'instructions' && c.state === InstructionsState.Overridden)!;

        expect.soft(provider.getTreeItem(overridden).tooltip).toContain('Overridden');

        provider.dispose();
    });

    it('should show active/total count in category tooltip when all detected', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        __setConfigStore({ 'sharppilot.instructions.lang.csharp': false });

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'category' && r.name === 'Languages')!;
        const item = provider.getTreeItem(languages);
        const langEntries = catalog.all.filter(e => e.category === 'Languages');
        const active = langEntries.filter(e => e.settingId !== 'sharppilot.instructions.lang.csharp').length;

        expect.soft(item.tooltip).toBe(`Languages\n${active}/${langEntries.length} active`);

        provider.dispose();
    });

    it('should show active/total count in category tooltip with not-detected entries', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const roots = provider.getChildren();
        const general = roots.find(r => r.kind === 'category' && r.name === 'General')!;
        const item = provider.getTreeItem(general);
        const generalEntries = catalog.all.filter(e => e.category === 'General');
        const alwaysOn = generalEntries.filter(e => !e.contextKeys || e.contextKeys.length === 0).length;

        expect.soft(item.tooltip).toBe(`General\n${alwaysOn}/${generalEntries.length} active`);

        provider.dispose();
    });

    it('should count overridden instructions as active in category tooltip', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        vi.mocked(fakeDetector.getOverriddenSettingIds).mockReturnValue(new Set(['sharppilot.instructions.lang.csharp']));

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'category' && r.name === 'Languages')!;
        const item = provider.getTreeItem(languages);
        const langEntries = catalog.all.filter(e => e.category === 'Languages');

        expect.soft(item.tooltip).toBe(`Languages\n${langEntries.length}/${langEntries.length} active`);

        provider.dispose();
    });

    it('should set treeView description to enabled/total count', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        __setConfigStore({ 'sharppilot.instructions.lang.csharp': false });

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        const total = catalog.count;
        const enabled = catalog.all.filter(e => e.settingId !== 'sharppilot.instructions.lang.csharp').length;

        expect.soft(treeView.description).toBe(`${enabled}/${total}`);

        provider.dispose();
    });

    it('should exclude not-detected entries from enabled count in description', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new InstructionsTreeProvider(fakeDetector, catalog);
        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        const total = catalog.count;
        const alwaysOn = catalog.all.filter(e => !e.contextKeys || e.contextKeys.length === 0).length;

        expect.soft(treeView.description).toBe(`${alwaysOn}/${total}`);

        provider.dispose();
    });
});
