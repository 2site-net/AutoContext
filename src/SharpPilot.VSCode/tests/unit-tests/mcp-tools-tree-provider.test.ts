import { describe, it, expect, vi, beforeEach } from 'vitest';
import { __setConfigStore, TreeItemCollapsibleState, TreeItemCheckboxState } from './__mocks__/vscode';
import { McpToolsTreeProvider, ToolState } from '../../src/mcp-tools-tree-provider';

const fakeDetector = {
    get: vi.fn((_key: string) => false),
    onDidDetect: vi.fn(() => ({ dispose: vi.fn() })),
} as unknown as import('../../src/workspace-context-detector').WorkspaceContextDetector;

beforeEach(() => {
    vi.clearAllMocks();
    __setConfigStore({});
    vi.mocked(fakeDetector.get).mockReset();
});

describe('McpToolsTreeProvider', () => {
    it('should return category nodes as root elements', () => {
        const provider = new McpToolsTreeProvider(fakeDetector);
        const roots = provider.getChildren();

        const names = roots.map(r => r.kind === 'category' ? r.name : '');
        expect.soft(names).toEqual(['C#', '.NET', 'Git', 'EditorConfig', 'TypeScript']);

        provider.dispose();
    });

    it('should return tool nodes as children of a category', () => {
        const provider = new McpToolsTreeProvider(fakeDetector);
        const roots = provider.getChildren();
        const csharp = roots.find(r => r.kind === 'category' && r.name === 'C#')!;
        const children = provider.getChildren(csharp);

        expect.soft(children.length).toBeGreaterThan(0);
        expect.soft(children.every(c => c.kind === 'tool')).toBe(true);

        provider.dispose();
    });

    it('should return empty array for leaf nodes', () => {
        const provider = new McpToolsTreeProvider(fakeDetector);
        const roots = provider.getChildren();
        const csharp = roots.find(r => r.kind === 'category' && r.name === 'C#')!;
        const children = provider.getChildren(csharp);

        expect.soft(provider.getChildren(children[0])).toEqual([]);

        provider.dispose();
    });

    it('should mark tools as not-detected when context is missing', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector);
        const roots = provider.getChildren();
        const csharp = roots.find(r => r.kind === 'category' && r.name === 'C#')!;
        const children = provider.getChildren(csharp);

        expect.soft(children.every(c => c.kind === 'tool' && c.state === ToolState.NotDetected)).toBe(true);

        provider.dispose();
    });

    it('should mark tools as enabled when context is detected and setting is true', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector);
        const roots = provider.getChildren();
        const csharp = roots.find(r => r.kind === 'category' && r.name === 'C#')!;
        const children = provider.getChildren(csharp);

        expect.soft(children.every(c => c.kind === 'tool' && c.state === ToolState.Enabled)).toBe(true);

        provider.dispose();
    });

    it('should mark tools as disabled when setting is false', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        __setConfigStore({ 'sharppilot.tools.check_csharp_async_patterns': false });

        const provider = new McpToolsTreeProvider(fakeDetector);
        const roots = provider.getChildren();
        const csharp = roots.find(r => r.kind === 'category' && r.name === 'C#')!;
        const children = provider.getChildren(csharp);

        const asyncTool = children.find(c => c.kind === 'tool' && c.entry.settingId === 'sharppilot.tools.check_csharp_async_patterns');
        expect.soft(asyncTool?.kind === 'tool' && asyncTool.state).toBe(ToolState.Disabled);

        provider.dispose();
    });

    it('should show checkbox checked for enabled tools', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector);
        const roots = provider.getChildren();
        const csharp = roots.find(r => r.kind === 'category' && r.name === 'C#')!;
        const children = provider.getChildren(csharp);
        const item = provider.getTreeItem(children[0]);

        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Checked);

        provider.dispose();
    });

    it('should show checkbox unchecked for disabled tools', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        __setConfigStore({ 'sharppilot.tools.check_csharp_async_patterns': false });

        const provider = new McpToolsTreeProvider(fakeDetector);
        const roots = provider.getChildren();
        const csharp = roots.find(r => r.kind === 'category' && r.name === 'C#')!;
        const children = provider.getChildren(csharp);
        const asyncTool = children.find(c => c.kind === 'tool' && c.entry.settingId === 'sharppilot.tools.check_csharp_async_patterns')!;
        const item = provider.getTreeItem(asyncTool);

        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Unchecked);

        provider.dispose();
    });

    it('should not show checkbox for not-detected tools', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector);
        const roots = provider.getChildren();
        const csharp = roots.find(r => r.kind === 'category' && r.name === 'C#')!;
        const children = provider.getChildren(csharp);
        const item = provider.getTreeItem(children[0]);

        expect.soft(item.checkboxState).toBeUndefined();
        expect.soft(item.description).toBe('not detected');

        provider.dispose();
    });

    it('should show category items as expanded with contextValue', () => {
        const provider = new McpToolsTreeProvider(fakeDetector);
        const roots = provider.getChildren();
        const item = provider.getTreeItem(roots[0]);

        expect.soft(item.collapsibleState).toBe(TreeItemCollapsibleState.Expanded);
        expect.soft(item.contextValue).toBe('category');

        provider.dispose();
    });

    it('should sort enabled before disabled before not-detected', () => {
        vi.mocked(fakeDetector.get).mockImplementation((key: string) => key === 'hasCSharp');
        __setConfigStore({ 'sharppilot.tools.check_csharp_async_patterns': false });

        const provider = new McpToolsTreeProvider(fakeDetector);
        const roots = provider.getChildren();
        const csharp = roots.find(r => r.kind === 'category' && r.name === 'C#')!;
        const children = provider.getChildren(csharp);

        const states = children.map(c => c.kind === 'tool' ? c.state : '');
        const enabledIdx = states.indexOf(ToolState.Enabled);
        const disabledIdx = states.indexOf(ToolState.Disabled);

        expect.soft(enabledIdx).toBeLessThan(disabledIdx);

        provider.dispose();
    });

    it('should include tools without context keys as enabled by default', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector);
        const roots = provider.getChildren();
        const editorConfig = roots.find(r => r.kind === 'category' && r.name === 'EditorConfig')!;
        const children = provider.getChildren(editorConfig);

        expect.soft(children.length).toBe(1);
        const tool = children[0];
        expect.soft(tool.kind === 'tool' && tool.state).toBe(ToolState.Enabled);

        provider.dispose();
    });
});
