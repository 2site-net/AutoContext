import { describe, it, expect, vi, beforeEach } from 'vitest';
import { __setConfigStore, TreeItemCollapsibleState, TreeItemCheckboxState, workspace, ConfigurationTarget, window } from './__mocks__/vscode';
import { McpToolsTreeProvider, ToolState } from '../../src/mcp-tools-tree-provider';
import { McpToolsRegistry } from '../../src/mcp-tools-registry';

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
    /** Navigate: root → group → category → aggregation tools */
    function getAggregationTools(provider: McpToolsTreeProvider, groupName: string, categoryName: string) {
        const groups = provider.getChildren();
        const group = groups.find(r => r.kind === 'group' && r.name === groupName)!;
        const categories = provider.getChildren(group);
        const category = categories.find(r => r.kind === 'category' && r.name === categoryName)!;
        return provider.getChildren(category);
    }

    /** Navigate: root → group → category → aggregation tool → sub-checks */
    function getSubChecks(provider: McpToolsTreeProvider, groupName: string, categoryName: string, toolName: string) {
        const tools = getAggregationTools(provider, groupName, categoryName);
        const tool = tools.find(r => r.kind === 'aggregationTool' && r.toolName === toolName)!;
        return provider.getChildren(tool);
    }

    it('should return group nodes as root elements', () => {
        const provider = new McpToolsTreeProvider(fakeDetector);
        const roots = provider.getChildren();

        const names = roots.map(r => r.kind === 'group' ? r.name : '');
        expect.soft(names).toEqual(['.NET', 'Web', 'Workspace']);

        provider.dispose();
    });

    it('should return category nodes as children of a group', () => {
        const provider = new McpToolsTreeProvider(fakeDetector);
        const roots = provider.getChildren();
        const dotnet = roots.find(r => r.kind === 'group' && r.name === '.NET')!;
        const categories = provider.getChildren(dotnet);

        const names = categories.map(r => r.kind === 'category' ? r.name : '');
        expect.soft(names).toEqual(['C#', 'NuGet']);

        provider.dispose();
    });

    it('should return aggregation tool nodes as children of a category', () => {
        const provider = new McpToolsTreeProvider(fakeDetector);
        const tools = getAggregationTools(provider, '.NET', 'C#');

        expect.soft(tools.length).toBe(1);
        expect.soft(tools[0].kind).toBe('aggregationTool');
        expect.soft(tools[0].kind === 'aggregationTool' && tools[0].toolName).toBe('check_csharp_all');

        provider.dispose();
    });

    it('should return sub-check nodes as children of a parent aggregation tool', () => {
        const provider = new McpToolsTreeProvider(fakeDetector);
        const subChecks = getSubChecks(provider, '.NET', 'C#', 'check_csharp_all');

        expect.soft(subChecks.length).toBeGreaterThan(0);
        expect.soft(subChecks.every(c => c.kind === 'subCheck')).toBe(true);

        provider.dispose();
    });

    it('should return empty array for leaf aggregation tool children', () => {
        const provider = new McpToolsTreeProvider(fakeDetector);
        const tools = getAggregationTools(provider, 'Workspace', 'EditorConfig');
        const leaf = tools.find(r => r.kind === 'aggregationTool' && r.toolName === 'get_editorconfig')!;

        expect.soft(provider.getChildren(leaf)).toEqual([]);

        provider.dispose();
    });

    it('should return empty array for sub-check children', () => {
        const provider = new McpToolsTreeProvider(fakeDetector);
        const subChecks = getSubChecks(provider, '.NET', 'C#', 'check_csharp_all');

        expect.soft(provider.getChildren(subChecks[0])).toEqual([]);

        provider.dispose();
    });

    it('should mark sub-checks as not-detected when context is missing', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector);
        const subChecks = getSubChecks(provider, '.NET', 'C#', 'check_csharp_all');

        expect.soft(subChecks.every(c => c.kind === 'subCheck' && c.state === ToolState.NotDetected)).toBe(true);

        provider.dispose();
    });

    it('should mark sub-checks as enabled when context is detected and setting is true', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector);
        const subChecks = getSubChecks(provider, '.NET', 'C#', 'check_csharp_all');

        expect.soft(subChecks.every(c => c.kind === 'subCheck' && c.state === ToolState.Enabled)).toBe(true);

        provider.dispose();
    });

    it('should mark sub-checks as disabled when setting is false', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        __setConfigStore({ 'sharppilot.tools.check_csharp_async_patterns': false });

        const provider = new McpToolsTreeProvider(fakeDetector);
        const subChecks = getSubChecks(provider, '.NET', 'C#', 'check_csharp_all');

        const asyncTool = subChecks.find(c => c.kind === 'subCheck' && c.entry.settingId === 'sharppilot.tools.check_csharp_async_patterns');
        expect.soft(asyncTool?.kind === 'subCheck' && asyncTool.state).toBe(ToolState.Disabled);

        provider.dispose();
    });

    it('should show checkbox checked for enabled sub-checks', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector);
        const subChecks = getSubChecks(provider, '.NET', 'C#', 'check_csharp_all');
        const item = provider.getTreeItem(subChecks[0]);

        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Checked);

        provider.dispose();
    });

    it('should show checkbox unchecked for disabled sub-checks', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        __setConfigStore({ 'sharppilot.tools.check_csharp_async_patterns': false });

        const provider = new McpToolsTreeProvider(fakeDetector);
        const subChecks = getSubChecks(provider, '.NET', 'C#', 'check_csharp_all');
        const asyncTool = subChecks.find(c => c.kind === 'subCheck' && c.entry.settingId === 'sharppilot.tools.check_csharp_async_patterns')!;
        const item = provider.getTreeItem(asyncTool);

        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Unchecked);

        provider.dispose();
    });

    it('should not show checkbox for not-detected sub-checks', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector);
        const subChecks = getSubChecks(provider, '.NET', 'C#', 'check_csharp_all');
        const item = provider.getTreeItem(subChecks[0]);

        expect.soft(item.checkboxState).toBeUndefined();
        expect.soft(item.description).toBe('not detected');

        provider.dispose();
    });

    it('should show group items as expanded with contextValue', () => {
        const provider = new McpToolsTreeProvider(fakeDetector);
        const roots = provider.getChildren();
        const item = provider.getTreeItem(roots[0]);

        expect.soft(item.collapsibleState).toBe(TreeItemCollapsibleState.Expanded);
        expect.soft(item.contextValue).toBe('group');

        provider.dispose();
    });

    it('should show category items as expanded with contextValue and no checkbox', () => {
        const provider = new McpToolsTreeProvider(fakeDetector);
        const roots = provider.getChildren();
        const dotnet = roots.find(r => r.kind === 'group' && r.name === '.NET')!;
        const categories = provider.getChildren(dotnet);
        const item = provider.getTreeItem(categories[0]);

        expect.soft(item.collapsibleState).toBe(TreeItemCollapsibleState.Expanded);
        expect.soft(item.contextValue).toBe('category');
        expect.soft(item.checkboxState).toBeUndefined();

        provider.dispose();
    });

    it('should show aggregation tool checkbox checked when all sub-checks are enabled', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector);
        const tools = getAggregationTools(provider, '.NET', 'C#');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Checked);

        provider.dispose();
    });

    it('should show aggregation tool checkbox unchecked when any sub-check is disabled', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        __setConfigStore({ 'sharppilot.tools.check_csharp_async_patterns': false });

        const provider = new McpToolsTreeProvider(fakeDetector);
        const tools = getAggregationTools(provider, '.NET', 'C#');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Unchecked);

        provider.dispose();
    });

    it('should show aggregation tool checkbox undefined when all sub-checks are not-detected', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector);
        const tools = getAggregationTools(provider, '.NET', 'C#');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.checkboxState).toBeUndefined();

        provider.dispose();
    });

    it('should show parent aggregation tool as expanded with contextValue', () => {
        const provider = new McpToolsTreeProvider(fakeDetector);
        const tools = getAggregationTools(provider, '.NET', 'C#');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.collapsibleState).toBe(TreeItemCollapsibleState.Expanded);
        expect.soft(item.contextValue).toBe('aggregationTool');

        provider.dispose();
    });

    it('should show leaf aggregation tool as non-collapsible with contextValue', () => {
        const provider = new McpToolsTreeProvider(fakeDetector);
        const tools = getAggregationTools(provider, 'Workspace', 'EditorConfig');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.collapsibleState).toBe(TreeItemCollapsibleState.None);
        expect.soft(item.contextValue).toBe('aggregationTool');

        provider.dispose();
    });

    it('should show leaf aggregation tool checkbox checked when enabled', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector);
        const tools = getAggregationTools(provider, 'Workspace', 'EditorConfig');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Checked);

        provider.dispose();
    });

    it('should sort enabled before disabled before not-detected in sub-checks', () => {
        vi.mocked(fakeDetector.get).mockImplementation((key: string) => key === 'hasCSharp');
        __setConfigStore({ 'sharppilot.tools.check_csharp_async_patterns': false });

        const provider = new McpToolsTreeProvider(fakeDetector);
        const subChecks = getSubChecks(provider, '.NET', 'C#', 'check_csharp_all');

        const states = subChecks.map(c => c.kind === 'subCheck' ? c.state : '');
        const enabledIdx = states.indexOf(ToolState.Enabled);
        const disabledIdx = states.indexOf(ToolState.Disabled);

        expect.soft(enabledIdx).toBeLessThan(disabledIdx);

        provider.dispose();
    });

    it('should include tools without context keys as enabled by default', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector);
        const tools = getAggregationTools(provider, 'Workspace', 'EditorConfig');

        expect.soft(tools.length).toBe(1);
        const item = provider.getTreeItem(tools[0]);
        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Checked);

        provider.dispose();
    });

    it('should hide not-detected tools when showNotDetected is false', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector);
        provider.showNotDetected = false;

        const roots = provider.getChildren();
        // Only Workspace group should remain (EditorConfig has no context keys)
        const names = roots.map(r => r.kind === 'group' ? r.name : '');
        expect.soft(names).toEqual(['Workspace']);

        provider.dispose();
    });

    it('should show not-detected tools when showNotDetected is true', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector);
        provider.showNotDetected = true;

        const roots = provider.getChildren();
        const names = roots.map(r => r.kind === 'group' ? r.name : '');
        expect.soft(names).toEqual(['.NET', 'Web', 'Workspace']);

        provider.dispose();
    });

    it('should include state description in tooltip for enabled sub-checks', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector);
        const subChecks = getSubChecks(provider, '.NET', 'C#', 'check_csharp_all');
        const enabledItem = provider.getTreeItem(subChecks[0]);
        expect.soft(enabledItem.tooltip).toContain('Enabled');

        provider.dispose();
    });

    it('should include state description in tooltip for disabled sub-checks', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        __setConfigStore({ 'sharppilot.tools.check_csharp_async_patterns': false });

        const provider = new McpToolsTreeProvider(fakeDetector);
        const subChecks = getSubChecks(provider, '.NET', 'C#', 'check_csharp_all');
        const disabled = subChecks.find(c => c.kind === 'subCheck' && c.state === ToolState.Disabled)!;

        expect.soft(provider.getTreeItem(disabled).tooltip).toContain('Disabled');

        provider.dispose();
    });

    it('should include state description in tooltip for not-detected sub-checks', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector);
        const subChecks = getSubChecks(provider, '.NET', 'C#', 'check_csharp_all');
        const notDetected = subChecks.find(c => c.kind === 'subCheck' && c.state === ToolState.NotDetected)!;

        expect.soft(provider.getTreeItem(notDetected).tooltip).toContain('Not detected');

        provider.dispose();
    });

    it('should include setting ID in sub-check tooltip', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector);
        const subChecks = getSubChecks(provider, '.NET', 'C#', 'check_csharp_all');
        const item = provider.getTreeItem(subChecks[0]);

        expect.soft(item.tooltip).toContain('sharppilot.tools.');

        provider.dispose();
    });

    it('should include sub-check count in parent aggregation tool tooltip', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector);
        const tools = getAggregationTools(provider, '.NET', 'C#');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.tooltip).toContain('check_csharp_all');
        expect.soft(item.tooltip).toContain('sub-checks enabled');

        provider.dispose();
    });

    it('should update sub-check setting when handleCheckboxChange fires for a sub-check', async () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector);
        const subChecks = getSubChecks(provider, '.NET', 'C#', 'check_csharp_all');
        const asyncTool = subChecks.find(c => c.kind === 'subCheck' && c.entry.settingId === 'sharppilot.tools.check_csharp_async_patterns')!;

        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        const checkboxCallback = vi.mocked(treeView.onDidChangeCheckboxState).mock.calls[0][0] as (e: { items: [typeof asyncTool, TreeItemCheckboxState][] }) => void;
        checkboxCallback({ items: [[asyncTool, TreeItemCheckboxState.Unchecked]] });

        await Promise.resolve();

        const config = vi.mocked(workspace.getConfiguration).mock.results.at(-1)!.value;
        expect.soft(config.update).toHaveBeenCalledWith(
            'sharppilot.tools.check_csharp_async_patterns',
            false,
            ConfigurationTarget.Global,
        );

        provider.dispose();
    });

    it('should update all detected sub-checks when handleCheckboxChange fires for a parent aggregation tool', async () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector);
        const tools = getAggregationTools(provider, '.NET', 'C#');
        const csharpTool = tools.find(r => r.kind === 'aggregationTool' && r.toolName === 'check_csharp_all')!;

        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        const checkboxCallback = vi.mocked(treeView.onDidChangeCheckboxState).mock.calls[0][0] as (e: { items: [typeof csharpTool, TreeItemCheckboxState][] }) => void;

        // Capture the index of the next getConfiguration() call — that will be the config used for updates
        const callsBefore = vi.mocked(workspace.getConfiguration).mock.calls.length;
        checkboxCallback({ items: [[csharpTool, TreeItemCheckboxState.Unchecked]] });
        await Promise.resolve();

        const config = vi.mocked(workspace.getConfiguration).mock.results[callsBefore]!.value;
        const subChecks = getSubChecks(provider, '.NET', 'C#', 'check_csharp_all');
        for (const sub of subChecks) {
            if (sub.kind === 'subCheck') {
                expect.soft(config.update).toHaveBeenCalledWith(sub.entry.settingId, false, ConfigurationTarget.Global);
            }
        }

        provider.dispose();
    });

    it('should skip not-detected sub-checks when aggregation tool bulk toggle fires', async () => {
        // hasCSharp=true so C# sub-checks are detected; hasTypeScript=false so TypeScript sub-checks are not.
        vi.mocked(fakeDetector.get).mockImplementation((key: string) => key === 'hasCSharp');

        const provider = new McpToolsTreeProvider(fakeDetector);
        const tools = getAggregationTools(provider, 'Web', 'TypeScript');

        // TypeScript aggregation tool: all sub-checks are not-detected (hasTypeScript=false)
        const tsTool = tools.find(r => r.kind === 'aggregationTool' && r.toolName === 'check_typescript_all')!;

        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        const checkboxCallback = vi.mocked(treeView.onDidChangeCheckboxState).mock.calls[0][0] as (e: { items: [typeof tsTool, TreeItemCheckboxState][] }) => void;

        const callsBefore = vi.mocked(workspace.getConfiguration).mock.calls.length;
        checkboxCallback({ items: [[tsTool, TreeItemCheckboxState.Checked]] });
        await Promise.resolve();

        const config = vi.mocked(workspace.getConfiguration).mock.results[callsBefore]!.value;

        // No TypeScript sub-checks should have been updated since they are all not-detected
        const subChecks = provider.getChildren(tsTool);
        for (const sub of subChecks) {
            if (sub.kind === 'subCheck') {
                expect.soft(config.update).not.toHaveBeenCalledWith(sub.entry.settingId, expect.anything(), expect.anything());
            }
        }

        provider.dispose();
    });

    it('should update leaf aggregation tool setting when handleCheckboxChange fires', async () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector);
        const tools = getAggregationTools(provider, 'Workspace', 'EditorConfig');
        const editorConfigTool = tools.find(r => r.kind === 'aggregationTool' && r.toolName === 'get_editorconfig')!;

        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        const checkboxCallback = vi.mocked(treeView.onDidChangeCheckboxState).mock.calls[0][0] as (e: { items: [typeof editorConfigTool, TreeItemCheckboxState][] }) => void;

        const callsBefore = vi.mocked(workspace.getConfiguration).mock.calls.length;
        checkboxCallback({ items: [[editorConfigTool, TreeItemCheckboxState.Unchecked]] });

        await Promise.resolve();

        const config = vi.mocked(workspace.getConfiguration).mock.results[callsBefore]!.value;
        expect.soft(config.update).toHaveBeenCalledWith(
            'sharppilot.tools.get_editorconfig',
            false,
            ConfigurationTarget.Global,
        );

        provider.dispose();
    });

    it('should set treeView description to enabled/total count', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        __setConfigStore({ 'sharppilot.tools.check_csharp_async_patterns': false });

        const provider = new McpToolsTreeProvider(fakeDetector);
        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        const total = McpToolsRegistry.count;
        const enabled = McpToolsRegistry.all.filter(e => e.settingId !== 'sharppilot.tools.check_csharp_async_patterns').length;

        expect.soft(treeView.description).toBe(`${enabled}/${total}`);

        provider.dispose();
    });
});
