import { describe, it, expect, vi, beforeEach } from 'vitest';
import { __setConfigStore, TreeItemCollapsibleState, TreeItemCheckboxState, workspace, ConfigurationTarget, window } from './__mocks__/vscode';
import { McpToolsTreeProvider } from '../../src/mcp-tools-tree-provider';
import { TreeViewNodeState } from '../../src/tree-view-node-state';
import { McpToolsCatalog } from '../../src/mcp-tools-catalog';
import { mcpTools } from '../../src/ui-constants';
import { TreeViewStateResolver } from '../../src/tree-view-state-resolver';
import { TreeViewTooltip } from '../../src/tree-view-tooltip';

const fakeDetector = {
    get: vi.fn((_key: string) => false),
    onDidDetect: vi.fn(() => ({ dispose: vi.fn() })),
} as unknown as import('../../src/workspace-context-detector').WorkspaceContextDetector;

const stateResolver = new TreeViewStateResolver(fakeDetector);
const tooltip = new TreeViewTooltip('tools');

beforeEach(() => {
    vi.clearAllMocks();
    __setConfigStore({});
    vi.mocked(fakeDetector.get).mockReset();
});

describe('McpToolsTreeProvider', () => {
    const catalog = new McpToolsCatalog(mcpTools);

    /** Navigate: root → group → category → MCP tools */
    function getMcpTools(provider: McpToolsTreeProvider, groupName: string, categoryName: string) {
        const groups = provider.getChildren();
        const group = groups.find(r => r.kind === 'group' && r.name === groupName)!;
        const categories = provider.getChildren(group);
        const category = categories.find(r => r.kind === 'category' && r.name === categoryName)!;
        return provider.getChildren(category);
    }

    /** Navigate: root → group → category → MCP tool → features */
    function getFeatures(provider: McpToolsTreeProvider, groupName: string, categoryName: string, toolName: string) {
        const tools = getMcpTools(provider, groupName, categoryName);
        const tool = tools.find(r => r.kind === 'mcpTool' && r.toolName === toolName)!;
        return provider.getChildren(tool);
    }

    it('should return group nodes as root elements', () => {
        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const roots = provider.getChildren();

        const names = roots.map(r => r.kind === 'group' ? r.name : '');
        expect.soft(names).toEqual(['.NET', 'Web', 'Workspace']);

        provider.dispose();
    });

    it('should return category nodes as children of a group', () => {
        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const roots = provider.getChildren();
        const dotnet = roots.find(r => r.kind === 'group' && r.name === '.NET')!;
        const categories = provider.getChildren(dotnet);

        const names = categories.map(r => r.kind === 'category' ? r.name : '');
        expect.soft(names).toEqual(['C#', 'NuGet']);

        provider.dispose();
    });

    it('should return MCP tool nodes as children of a category', () => {
        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const tools = getMcpTools(provider, '.NET', 'C#');

        expect.soft(tools.length).toBe(1);
        expect.soft(tools[0].kind).toBe('mcpTool');
        expect.soft(tools[0].kind === 'mcpTool' && tools[0].toolName).toBe('check_csharp_all');

        provider.dispose();
    });

    it('should return feature nodes as children of a parent MCP tool', () => {
        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');

        expect.soft(features.length).toBeGreaterThan(0);
        expect.soft(features.every(c => c.kind === 'mcpToolFeature')).toBe(true);

        provider.dispose();
    });

    it('should return empty array for leaf MCP tool children', () => {
        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const tools = getMcpTools(provider, 'Workspace', 'EditorConfig');
        const leaf = tools.find(r => r.kind === 'mcpTool' && r.toolName === 'get_editorconfig')!;

        expect.soft(provider.getChildren(leaf)).toEqual([]);

        provider.dispose();
    });

    it('should return empty array for feature node children', () => {
        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');

        expect.soft(provider.getChildren(features[0])).toEqual([]);

        provider.dispose();
    });

    it('should mark features as not-detected when context is missing', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');

        expect.soft(features.every(c => c.kind === 'mcpToolFeature' && c.state === TreeViewNodeState.NotDetected)).toBe(true);

        provider.dispose();
    });

    it('should mark features as enabled when context is detected and setting is true', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');

        expect.soft(features.every(c => c.kind === 'mcpToolFeature' && c.state === TreeViewNodeState.Enabled)).toBe(true);

        provider.dispose();
    });

    it('should mark features as disabled when setting is false', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        __setConfigStore({ 'autocontext.mcpTools.check_csharp_async_patterns': false });

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');

        const asyncTool = features.find(c => c.kind === 'mcpToolFeature' && c.entry.settingId === 'autocontext.mcpTools.check_csharp_async_patterns');
        expect.soft(asyncTool?.kind === 'mcpToolFeature' && asyncTool.state).toBe(TreeViewNodeState.Disabled);

        provider.dispose();
    });

    it('should show checkbox checked for enabled features', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');
        const item = provider.getTreeItem(features[0]);

        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Checked);

        provider.dispose();
    });

    it('should show checkbox unchecked for disabled features', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        __setConfigStore({ 'autocontext.mcpTools.check_csharp_async_patterns': false });

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');
        const asyncTool = features.find(c => c.kind === 'mcpToolFeature' && c.entry.settingId === 'autocontext.mcpTools.check_csharp_async_patterns')!;
        const item = provider.getTreeItem(asyncTool);

        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Unchecked);

        provider.dispose();
    });

    it('should not show checkbox for not-detected features', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');
        const item = provider.getTreeItem(features[0]);

        expect.soft(item.checkboxState).toBeUndefined();
        expect.soft(item.description).toBe('not detected');

        provider.dispose();
    });

    it('should show group items as expanded with contextValue', () => {
        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const roots = provider.getChildren();
        const item = provider.getTreeItem(roots[0]);

        expect.soft(item.collapsibleState).toBe(TreeItemCollapsibleState.Expanded);
        expect.soft(item.contextValue).toBe('group');

        provider.dispose();
    });

    it('should show category items as expanded with contextValue and no checkbox', () => {
        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const roots = provider.getChildren();
        const dotnet = roots.find(r => r.kind === 'group' && r.name === '.NET')!;
        const categories = provider.getChildren(dotnet);
        const item = provider.getTreeItem(categories[0]);

        expect.soft(item.collapsibleState).toBe(TreeItemCollapsibleState.Expanded);
        expect.soft(item.contextValue).toBe('category');
        expect.soft(item.checkboxState).toBeUndefined();

        provider.dispose();
    });

    it('should show MCP tool checkbox checked when all features are enabled', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const tools = getMcpTools(provider, '.NET', 'C#');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Checked);

        provider.dispose();
    });

    it('should show MCP tool checkbox unchecked when any feature is disabled', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        __setConfigStore({ 'autocontext.mcpTools.check_csharp_async_patterns': false });

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const tools = getMcpTools(provider, '.NET', 'C#');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Unchecked);

        provider.dispose();
    });

    it('should show MCP tool checkbox undefined when all features are not-detected', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const tools = getMcpTools(provider, '.NET', 'C#');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.checkboxState).toBeUndefined();

        provider.dispose();
    });

    it('should show parent MCP tool as expanded with contextValue', () => {
        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const tools = getMcpTools(provider, '.NET', 'C#');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.collapsibleState).toBe(TreeItemCollapsibleState.Expanded);
        expect.soft(item.contextValue).toBe('mcpTool');

        provider.dispose();
    });

    it('should show leaf MCP tool as non-collapsible with contextValue', () => {
        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const tools = getMcpTools(provider, 'Workspace', 'EditorConfig');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.collapsibleState).toBe(TreeItemCollapsibleState.None);
        expect.soft(item.contextValue).toBe('mcpTool');

        provider.dispose();
    });

    it('should show leaf MCP tool checkbox checked when enabled', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const tools = getMcpTools(provider, 'Workspace', 'EditorConfig');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Checked);

        provider.dispose();
    });

    it('should sort enabled before disabled before not-detected in features', () => {
        vi.mocked(fakeDetector.get).mockImplementation((key: string) => key === 'hasCSharp');
        __setConfigStore({ 'autocontext.mcpTools.check_csharp_async_patterns': false });

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');

        const states = features.map(c => c.kind === 'mcpToolFeature' ? c.state : '');
        const enabledIdx = states.indexOf(TreeViewNodeState.Enabled);
        const disabledIdx = states.indexOf(TreeViewNodeState.Disabled);

        expect.soft(enabledIdx).toBeLessThan(disabledIdx);

        provider.dispose();
    });

    it('should include tools without context keys as enabled by default', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const tools = getMcpTools(provider, 'Workspace', 'EditorConfig');

        expect.soft(tools.length).toBe(1);
        const item = provider.getTreeItem(tools[0]);
        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Checked);

        provider.dispose();
    });

    it('should hide not-detected tools when showNotDetected is false', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        provider.showNotDetected = false;

        const roots = provider.getChildren();
        // Only Workspace group should remain (EditorConfig has no context keys)
        const names = roots.map(r => r.kind === 'group' ? r.name : '');
        expect.soft(names).toEqual(['Workspace']);

        provider.dispose();
    });

    it('should show not-detected tools when showNotDetected is true', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        provider.showNotDetected = true;

        const roots = provider.getChildren();
        const names = roots.map(r => r.kind === 'group' ? r.name : '');
        expect.soft(names).toEqual(['.NET', 'Web', 'Workspace']);

        provider.dispose();
    });

    it('should include state description in tooltip for enabled features', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');
        const enabledItem = provider.getTreeItem(features[0]);
        expect.soft(enabledItem.tooltip).toContain('Enabled');

        provider.dispose();
    });

    it('should include state description in tooltip for disabled features', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        __setConfigStore({ 'autocontext.mcpTools.check_csharp_async_patterns': false });

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');
        const disabled = features.find(c => c.kind === 'mcpToolFeature' && c.state === TreeViewNodeState.Disabled)!;

        expect.soft(provider.getTreeItem(disabled).tooltip).toContain('Disabled');

        provider.dispose();
    });

    it('should include state description in tooltip for not-detected features', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');
        const notDetected = features.find(c => c.kind === 'mcpToolFeature' && c.state === TreeViewNodeState.NotDetected)!;

        expect.soft(provider.getTreeItem(notDetected).tooltip).toContain('Not detected');

        provider.dispose();
    });

    it('should include setting ID in feature tooltip', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');
        const item = provider.getTreeItem(features[0]);

        expect.soft(item.tooltip).toContain('autocontext.mcpTools.');

        provider.dispose();
    });

    it('should include feature count in parent MCP tool tooltip', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const tools = getMcpTools(provider, '.NET', 'C#');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.tooltip).toContain('check_csharp_all');
        expect.soft(item.tooltip).toContain('features enabled');

        provider.dispose();
    });

    it('should update feature setting when handleCheckboxChange fires for a feature', async () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');
        const asyncTool = features.find(c => c.kind === 'mcpToolFeature' && c.entry.settingId === 'autocontext.mcpTools.check_csharp_async_patterns')!;

        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        const checkboxCallback = vi.mocked(treeView.onDidChangeCheckboxState).mock.calls[0][0] as (e: { items: [typeof asyncTool, TreeItemCheckboxState][] }) => void;
        checkboxCallback({ items: [[asyncTool, TreeItemCheckboxState.Unchecked]] });

        await Promise.resolve();

        const config = vi.mocked(workspace.getConfiguration).mock.results.at(-1)!.value;
        expect.soft(config.update).toHaveBeenCalledWith(
            'autocontext.mcpTools.check_csharp_async_patterns',
            false,
            ConfigurationTarget.Global,
        );

        provider.dispose();
    });

    it('should update all detected features when handleCheckboxChange fires for a parent MCP tool', async () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const tools = getMcpTools(provider, '.NET', 'C#');
        const csharpTool = tools.find(r => r.kind === 'mcpTool' && r.toolName === 'check_csharp_all')!;

        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        const checkboxCallback = vi.mocked(treeView.onDidChangeCheckboxState).mock.calls[0][0] as (e: { items: [typeof csharpTool, TreeItemCheckboxState][] }) => void;

        // Capture the index of the next getConfiguration() call — that will be the config used for updates
        const callsBefore = vi.mocked(workspace.getConfiguration).mock.calls.length;
        checkboxCallback({ items: [[csharpTool, TreeItemCheckboxState.Unchecked]] });
        await Promise.resolve();

        const config = vi.mocked(workspace.getConfiguration).mock.results[callsBefore]!.value;
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');
        for (const sub of features) {
            if (sub.kind === 'mcpToolFeature') {
                expect.soft(config.update).toHaveBeenCalledWith(sub.entry.settingId, false, ConfigurationTarget.Global);
            }
        }

        provider.dispose();
    });

    it('should skip not-detected features when MCP tool bulk toggle fires', async () => {
        // hasCSharp=true so C# features are detected; hasTypeScript=false so TypeScript features are not.
        vi.mocked(fakeDetector.get).mockImplementation((key: string) => key === 'hasCSharp');

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const tools = getMcpTools(provider, 'Web', 'TypeScript');

        // TypeScript MCP tool: all features are not-detected (hasTypeScript=false)
        const tsTool = tools.find(r => r.kind === 'mcpTool' && r.toolName === 'check_typescript_all')!;

        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        const checkboxCallback = vi.mocked(treeView.onDidChangeCheckboxState).mock.calls[0][0] as (e: { items: [typeof tsTool, TreeItemCheckboxState][] }) => void;

        const callsBefore = vi.mocked(workspace.getConfiguration).mock.calls.length;
        checkboxCallback({ items: [[tsTool, TreeItemCheckboxState.Checked]] });
        await Promise.resolve();

        const config = vi.mocked(workspace.getConfiguration).mock.results[callsBefore]!.value;

        // No TypeScript features should have been updated since they are all not-detected
        const features = provider.getChildren(tsTool);
        for (const sub of features) {
            if (sub.kind === 'mcpToolFeature') {
                expect.soft(config.update).not.toHaveBeenCalledWith(sub.entry.settingId, expect.anything(), expect.anything());
            }
        }

        provider.dispose();
    });

    it('should update leaf MCP tool setting when handleCheckboxChange fires', async () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const tools = getMcpTools(provider, 'Workspace', 'EditorConfig');
        const editorConfigTool = tools.find(r => r.kind === 'mcpTool' && r.toolName === 'get_editorconfig')!;

        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        const checkboxCallback = vi.mocked(treeView.onDidChangeCheckboxState).mock.calls[0][0] as (e: { items: [typeof editorConfigTool, TreeItemCheckboxState][] }) => void;

        const callsBefore = vi.mocked(workspace.getConfiguration).mock.calls.length;
        checkboxCallback({ items: [[editorConfigTool, TreeItemCheckboxState.Unchecked]] });

        await Promise.resolve();

        const config = vi.mocked(workspace.getConfiguration).mock.results[callsBefore]!.value;
        expect.soft(config.update).toHaveBeenCalledWith(
            'autocontext.mcpTools.get_editorconfig',
            false,
            ConfigurationTarget.Global,
        );

        provider.dispose();
    });

    it('should show enabled/total count in group tooltip when all detected', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const roots = provider.getChildren();
        const dotnet = roots.find(r => r.kind === 'group' && r.name === '.NET')!;
        const item = provider.getTreeItem(dotnet);
        const dotnetEntries = catalog.all.filter(e => e.group === '.NET');

        expect.soft(item.tooltip).toBe(`.NET\n${dotnetEntries.length}/${dotnetEntries.length} features enabled`);

        provider.dispose();
    });

    it('should show enabled/total count in group tooltip with not-detected entries', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const roots = provider.getChildren();
        const workspace = roots.find(r => r.kind === 'group' && r.name === 'Workspace')!;
        const item = provider.getTreeItem(workspace);
        const workspaceEntries = catalog.all.filter(e => e.group === 'Workspace');
        const alwaysOn = workspaceEntries.filter(e => !e.contextKeys || e.contextKeys.length === 0).length;

        expect.soft(item.tooltip).toBe(`Workspace\n${alwaysOn}/${workspaceEntries.length} features enabled`);

        provider.dispose();
    });

    it('should show enabled/total count in category tooltip when all detected', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        __setConfigStore({ 'autocontext.mcpTools.check_csharp_async_patterns': false });

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const roots = provider.getChildren();
        const dotnet = roots.find(r => r.kind === 'group' && r.name === '.NET')!;
        const categories = provider.getChildren(dotnet);
        const csharp = categories.find(r => r.kind === 'category' && r.name === 'C#')!;
        const item = provider.getTreeItem(csharp);
        const csharpEntries = catalog.all.filter(e => e.category === 'C#');
        const enabled = csharpEntries.filter(e => e.settingId !== 'autocontext.mcpTools.check_csharp_async_patterns').length;

        expect.soft(item.tooltip).toBe(`C#\n${enabled}/${csharpEntries.length} features enabled`);

        provider.dispose();
    });

    it('should show enabled/total count in category tooltip with not-detected entries', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const roots = provider.getChildren();
        const dotnet = roots.find(r => r.kind === 'group' && r.name === '.NET')!;
        const categories = provider.getChildren(dotnet);
        const csharp = categories.find(r => r.kind === 'category' && r.name === 'C#')!;
        const item = provider.getTreeItem(csharp);
        const csharpEntries = catalog.all.filter(e => e.category === 'C#');

        expect.soft(item.tooltip).toBe(`C#\n0/${csharpEntries.length} features enabled`);

        provider.dispose();
    });

    it('should set treeView description to enabled/total count', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        __setConfigStore({ 'autocontext.mcpTools.check_csharp_async_patterns': false });

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        const total = catalog.count;
        const enabled = catalog.all.filter(e => e.settingId !== 'autocontext.mcpTools.check_csharp_async_patterns').length;

        expect.soft(treeView.description).toBe(`${enabled}/${total}`);

        provider.dispose();
    });

    it('should exclude not-detected entries from enabled count in description', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip);
        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        const total = catalog.count;
        const alwaysOn = catalog.all.filter(e => !e.contextKeys || e.contextKeys.length === 0).length;

        expect.soft(treeView.description).toBe(`${alwaysOn}/${total}`);

        provider.dispose();
    });
});
