import { describe, it, expect, vi, beforeEach } from 'vitest';
import { __setConfigStore, TreeItemCollapsibleState } from './__mocks__/vscode';
import { InstructionsTreeProvider, InstructionState } from '../../src/instructions-tree-provider';
import { InstructionsRegistry } from '../../src/instructions-registry';

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
    it('should return category nodes as root elements', () => {
        const provider = new InstructionsTreeProvider(fakeDetector);
        const roots = provider.getChildren();

        const names = roots.map(r => r.kind === 'category' ? r.name : '');
        expect.soft(names).toEqual(['General', 'Languages', '.NET', 'Web', 'Tools']);

        provider.dispose();
    });

    it('should return instruction nodes as children of a category', () => {
        const provider = new InstructionsTreeProvider(fakeDetector);
        const roots = provider.getChildren();
        const general = roots.find(r => r.kind === 'category' && r.name === 'General')!;
        const children = provider.getChildren(general);

        expect.soft(children.length).toBeGreaterThan(0);
        expect.soft(children.every(c => c.kind === 'instruction')).toBe(true);

        provider.dispose();
    });

    it('should return no children for an instruction node', () => {
        const provider = new InstructionsTreeProvider(fakeDetector);
        const roots = provider.getChildren();
        const general = roots.find(r => r.kind === 'category' && r.name === 'General')!;
        const children = provider.getChildren(general);
        const leaf = children[0];

        expect.soft(provider.getChildren(leaf)).toEqual([]);

        provider.dispose();
    });

    it('should show category items as expanded', () => {
        const provider = new InstructionsTreeProvider(fakeDetector);
        const roots = provider.getChildren();
        const treeItem = provider.getTreeItem(roots[0]);

        expect.soft(treeItem.collapsibleState).toBe(TreeItemCollapsibleState.Expanded);

        provider.dispose();
    });

    it('should mark instructions as active when config is enabled and context keys match', () => {
        vi.mocked(fakeDetector.get).mockImplementation((key: string) => key === 'hasCSharp');

        const provider = new InstructionsTreeProvider(fakeDetector);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'category' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const csharp = children.find(c => c.kind === 'instruction' && c.entry.settingId === 'sharppilot.instructions.lang.csharp')!;

        expect.soft(csharp.kind === 'instruction' && csharp.state).toBe(InstructionState.Active);

        provider.dispose();
    });

    it('should mark instructions as not detected when context keys do not match', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new InstructionsTreeProvider(fakeDetector);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'category' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const csharp = children.find(c => c.kind === 'instruction' && c.entry.settingId === 'sharppilot.instructions.lang.csharp')!;

        expect.soft(csharp.kind === 'instruction' && csharp.state).toBe(InstructionState.NotDetected);

        const treeItem = provider.getTreeItem(csharp);
        expect.soft(treeItem.description).toBe('not detected');

        provider.dispose();
    });

    it('should mark instructions as disabled when config setting is false', () => {
        __setConfigStore({ 'sharppilot.instructions.lang.csharp': false });

        const provider = new InstructionsTreeProvider(fakeDetector);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'category' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const csharp = children.find(c => c.kind === 'instruction' && c.entry.settingId === 'sharppilot.instructions.lang.csharp')!;

        expect.soft(csharp.kind === 'instruction' && csharp.state).toBe(InstructionState.Disabled);

        const treeItem = provider.getTreeItem(csharp);
        expect.soft(treeItem.description).toBe('disabled');

        provider.dispose();
    });

    it('should mark instructions as overridden when settingId is in overrides', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        vi.mocked(fakeDetector.getOverriddenSettingIds).mockReturnValue(new Set(['sharppilot.instructions.lang.csharp']));

        const provider = new InstructionsTreeProvider(fakeDetector);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'category' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const csharp = children.find(c => c.kind === 'instruction' && c.entry.settingId === 'sharppilot.instructions.lang.csharp')!;

        expect.soft(csharp.kind === 'instruction' && csharp.state).toBe(InstructionState.Overridden);

        const treeItem = provider.getTreeItem(csharp);
        expect.soft(treeItem.description).toBe('overridden');

        provider.dispose();
    });

    it('should treat always-on entries (no contextKeys) as active when enabled', () => {
        const provider = new InstructionsTreeProvider(fakeDetector);
        const roots = provider.getChildren();
        const general = roots.find(r => r.kind === 'category' && r.name === 'General')!;
        const children = provider.getChildren(general);
        const codeReview = children.find(c => c.kind === 'instruction' && c.entry.settingId === 'sharppilot.instructions.codeReview')!;

        expect.soft(codeReview.kind === 'instruction' && codeReview.state).toBe(InstructionState.Active);

        provider.dispose();
    });

    it('should preserve category order from the registry', () => {
        const provider = new InstructionsTreeProvider(fakeDetector);
        const roots = provider.getChildren();

        const categories = roots.filter(r => r.kind === 'category').map(r => r.kind === 'category' ? r.name : '');
        expect.soft(categories).toEqual(['General', 'Languages', '.NET', 'Web', 'Tools']);

        provider.dispose();
    });

    it('should sort instructions within a category by label (matching registry order)', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new InstructionsTreeProvider(fakeDetector);
        const roots = provider.getChildren();
        const general = roots.find(r => r.kind === 'category' && r.name === 'General')!;
        const children = provider.getChildren(general);
        const labels = children.map(c => c.kind === 'instruction' ? c.entry.label : '');
        const expected = InstructionsRegistry.all.filter(e => e.category === 'General').map(e => e.label);

        expect.soft(labels).toEqual(expected);

        provider.dispose();
    });

    it('should include tooltip with setting ID', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new InstructionsTreeProvider(fakeDetector);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'category' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);
        const csharp = children.find(c => c.kind === 'instruction' && c.entry.settingId === 'sharppilot.instructions.lang.csharp')!;

        const treeItem = provider.getTreeItem(csharp);
        expect.soft(treeItem.tooltip).toContain('sharppilot.instructions.lang.csharp');

        provider.dispose();
    });

    it('should sort active instructions before disabled, and disabled before not detected', () => {
        vi.mocked(fakeDetector.get).mockImplementation((key: string) => key === 'hasCSharp');
        __setConfigStore({ 'sharppilot.instructions.lang.typescript': false });

        const provider = new InstructionsTreeProvider(fakeDetector);
        const roots = provider.getChildren();
        const languages = roots.find(r => r.kind === 'category' && r.name === 'Languages')!;
        const children = provider.getChildren(languages);

        const states = children
            .filter(c => c.kind === 'instruction')
            .map(c => c.kind === 'instruction' ? c.state : '');

        const firstDisabledIndex = states.indexOf(InstructionState.Disabled);
        const firstNotDetectedIndex = states.indexOf(InstructionState.NotDetected);
        const lastActiveIndex = states.lastIndexOf(InstructionState.Active);

        if (lastActiveIndex !== -1 && firstDisabledIndex !== -1) {
            expect.soft(lastActiveIndex).toBeLessThan(firstDisabledIndex);
        }
        if (firstDisabledIndex !== -1 && firstNotDetectedIndex !== -1) {
            expect.soft(firstDisabledIndex).toBeLessThan(firstNotDetectedIndex);
        }

        provider.dispose();
    });
});
