import { describe, it, expect, vi, beforeEach } from 'vitest';
import { TreeItemCollapsibleState, TreeItemCheckboxState, window, ThemeIcon, ThemeColor } from './_fakes/fake-vscode';
import { McpToolsTreeProvider } from '../../src/mcp-tools-tree-provider';
import type { AutoContextConfig } from '../../src/types/autocontext-config';
import { TreeViewNodeState } from '../../src/tree-view-node-state';
import { McpToolsCatalog } from '../../src/mcp-tools-catalog';
import { mcpTools } from '../../src/ui-constants';
import { TreeViewStateResolver } from '../../src/tree-view-state-resolver';
import { TreeViewTooltip } from '../../src/tree-view-tooltip';
import { createFakeDetector, createFakeConfigManager, createFakeHealthMonitor, createFakeOutputChannel } from './_fakes';

const fakeDetector = createFakeDetector();
const outputChannel = createFakeOutputChannel();

const stateResolver = new TreeViewStateResolver(fakeDetector);
const tooltip = new TreeViewTooltip('tools');

let currentConfig: AutoContextConfig = {};
const fakeConfigManager = createFakeConfigManager();

beforeEach(() => {
    vi.clearAllMocks();
    currentConfig = {};
    vi.mocked(fakeConfigManager.readSync).mockImplementation(() => currentConfig);
    vi.mocked(fakeConfigManager.onDidChange).mockReturnValue({ dispose: vi.fn() });
    vi.mocked(fakeDetector.get).mockReset();
});

describe('McpToolsTreeProvider', () => {
    const catalog = new McpToolsCatalog(mcpTools);

    /** Navigate: root → serverLabel → category → MCP tools */
    function getMcpTools(provider: McpToolsTreeProvider, serverLabelName: string, categoryName: string) {
        const serverLabels = provider.getChildren();
        const serverLabel = serverLabels.find(r => r.kind === 'serverNode' && r.name === serverLabelName)!;
        const categories = provider.getChildren(serverLabel);
        const category = categories.find(r => r.kind === 'categoryNode' && r.name === categoryName)!;
        return provider.getChildren(category);
    }

    /** Navigate: root → serverLabel → category → MCP tool → features */
    function getFeatures(provider: McpToolsTreeProvider, serverLabelName: string, categoryName: string, toolName: string) {
        const tools = getMcpTools(provider, serverLabelName, categoryName);
        const tool = tools.find(r => r.kind === 'mcpToolNode' && r.toolName === toolName)!;
        return provider.getChildren(tool);
    }

    it('should return server nodes as root elements', () => {
        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();

        const names = roots.map(r => r.kind === 'serverNode' ? r.name : '');
        expect.soft(names).toEqual(['.NET', 'Web', 'Workspace']);

        provider.dispose();
    });

    it('should return category nodes as children of a server node', () => {
        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const dotnet = roots.find(r => r.kind === 'serverNode' && r.name === '.NET')!;
        const categories = provider.getChildren(dotnet);

        const names = categories.map(r => r.kind === 'categoryNode' ? r.name : '');
        expect.soft(names).toEqual(['C#', 'NuGet']);

        provider.dispose();
    });

    it('should return MCP tool nodes as children of a category', () => {
        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, '.NET', 'C#');

        expect.soft(tools.length).toBe(1);
        expect.soft(tools[0].kind).toBe('mcpToolNode');
        expect.soft(tools[0].kind === 'mcpToolNode' && tools[0].toolName).toBe('check_csharp_all');

        provider.dispose();
    });

    it('should return feature nodes as children of a parent MCP tool', () => {
        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');

        expect.soft(features.length).toBeGreaterThan(0);
        expect.soft(features.every(c => c.kind === 'mcpToolFeatureNode')).toBe(true);

        provider.dispose();
    });

    it('should return empty array for leaf MCP tool children', () => {
        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, 'Workspace', 'EditorConfig');
        const leaf = tools.find(r => r.kind === 'mcpToolNode' && r.toolName === 'get_editorconfig')!;

        expect.soft(provider.getChildren(leaf)).toEqual([]);

        provider.dispose();
    });

    it('should return empty array for feature node children', () => {
        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');

        expect.soft(provider.getChildren(features[0])).toEqual([]);

        provider.dispose();
    });

    it('should mark features as not-detected when context is missing', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');

        expect.soft(features.every(c => c.kind === 'mcpToolFeatureNode' && c.state === TreeViewNodeState.NotDetected)).toBe(true);

        provider.dispose();
    });

    it('should mark features as enabled when context is detected and setting is true', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');

        expect.soft(features.every(c => c.kind === 'mcpToolFeatureNode' && c.state === TreeViewNodeState.Enabled)).toBe(true);

        provider.dispose();
    });

    it('should mark features as disabled when setting is false', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        currentConfig = { mcpTools: { check_csharp_all: { disabledFeatures: ['check_csharp_async_patterns'] } } };

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');

        const asyncTool = features.find(c => c.kind === 'mcpToolFeatureNode' && c.entry.contextKey === 'autocontext.mcpTools.check_csharp_async_patterns');
        expect.soft(asyncTool?.kind === 'mcpToolFeatureNode' && asyncTool.state).toBe(TreeViewNodeState.Disabled);

        provider.dispose();
    });

    it('should show checkbox checked for enabled features', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');
        const item = provider.getTreeItem(features[0]);

        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Checked);

        provider.dispose();
    });

    it('should show checkbox unchecked for disabled features', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        currentConfig = { mcpTools: { check_csharp_all: { disabledFeatures: ['check_csharp_async_patterns'] } } };

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');
        const asyncTool = features.find(c => c.kind === 'mcpToolFeatureNode' && c.entry.contextKey === 'autocontext.mcpTools.check_csharp_async_patterns')!;
        const item = provider.getTreeItem(asyncTool);

        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Unchecked);

        provider.dispose();
    });

    it('should not show checkbox for not-detected features', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');
        const item = provider.getTreeItem(features[0]);

        expect.soft(item.checkboxState).toBeUndefined();
        expect.soft(item.description).toBe('not detected');

        provider.dispose();
    });

    it('should show server items as expanded with contextValue', () => {
        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const item = provider.getTreeItem(roots[0]);

        expect.soft(item.collapsibleState).toBe(TreeItemCollapsibleState.Expanded);
        expect.soft(item.contextValue).toBe('serverNode.stopped');

        provider.dispose();
    });

    it('should show category items as expanded with contextValue and no checkbox', () => {
        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const dotnet = roots.find(r => r.kind === 'serverNode' && r.name === '.NET')!;
        const categories = provider.getChildren(dotnet);
        const item = provider.getTreeItem(categories[0]);

        expect.soft(item.collapsibleState).toBe(TreeItemCollapsibleState.Expanded);
        expect.soft(item.contextValue).toBe('categoryNode');
        expect.soft(item.checkboxState).toBeUndefined();

        provider.dispose();
    });

    it('should show MCP tool checkbox checked when all features are enabled', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, '.NET', 'C#');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Checked);

        provider.dispose();
    });

    it('should show MCP tool checkbox checked when some features are disabled', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        currentConfig = { mcpTools: { check_csharp_all: { disabledFeatures: ['check_csharp_async_patterns'] } } };

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, '.NET', 'C#');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Checked);

        provider.dispose();
    });

    it('should show MCP tool checkbox checked when all features are disabled but parent is not', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        const allFeatures = catalog.all
            .filter(e => e.toolName === 'check_csharp_all' && e.featureName)
            .map(e => e.featureName!);
        currentConfig = { mcpTools: { check_csharp_all: { disabledFeatures: allFeatures } } };

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, '.NET', 'C#');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Checked);

        provider.dispose();
    });

    it('should show MCP tool checkbox unchecked when parent has enabled:false', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        currentConfig = { mcpTools: { check_csharp_all: { enabled: false } } };

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, '.NET', 'C#');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Unchecked);

        provider.dispose();
    });

    it('should show MCP tool checkbox unchecked when parent entry is false', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        currentConfig = { mcpTools: { check_csharp_all: false } };

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, '.NET', 'C#');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Unchecked);

        provider.dispose();
    });

    it('should show MCP tool checkbox undefined when all features are not-detected', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, '.NET', 'C#');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.checkboxState).toBeUndefined();

        provider.dispose();
    });

    it('should show parent MCP tool as expanded with contextValue', () => {
        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, '.NET', 'C#');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.collapsibleState).toBe(TreeItemCollapsibleState.Expanded);
        expect.soft(item.contextValue).toBe('mcpToolNode');

        provider.dispose();
    });

    it('should show leaf MCP tool as non-collapsible with contextValue', () => {
        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, 'Workspace', 'EditorConfig');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.collapsibleState).toBe(TreeItemCollapsibleState.None);
        expect.soft(item.contextValue).toBe('mcpToolNode');

        provider.dispose();
    });

    it('should show leaf MCP tool checkbox checked when enabled', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, 'Workspace', 'EditorConfig');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Checked);

        provider.dispose();
    });

    it('should sort enabled before disabled before not-detected in features', () => {
        vi.mocked(fakeDetector.get).mockImplementation((key: string) => key === 'hasCSharp');
        currentConfig = { mcpTools: { check_csharp_all: { disabledFeatures: ['check_csharp_async_patterns'] } } };

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');

        const states = features.map(c => c.kind === 'mcpToolFeatureNode' ? c.state : '');
        const enabledIdx = states.indexOf(TreeViewNodeState.Enabled);
        const disabledIdx = states.indexOf(TreeViewNodeState.Disabled);

        expect.soft(enabledIdx).toBeLessThan(disabledIdx);

        provider.dispose();
    });

    it('should include tools without context keys as enabled by default', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, 'Workspace', 'EditorConfig');

        expect.soft(tools.length).toBe(1);
        const item = provider.getTreeItem(tools[0]);
        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Checked);

        provider.dispose();
    });

    it('should hide not-detected tools when showNotDetected is false', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        provider.showNotDetected = false;

        const roots = provider.getChildren();
        // Only Workspace server should remain (EditorConfig has no context keys)
        const names = roots.map(r => r.kind === 'serverNode' ? r.name : '');
        expect.soft(names).toEqual(['Workspace']);

        provider.dispose();
    });

    it('should show not-detected tools when showNotDetected is true', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        provider.showNotDetected = true;

        const roots = provider.getChildren();
        const names = roots.map(r => r.kind === 'serverNode' ? r.name : '');
        expect.soft(names).toEqual(['.NET', 'Web', 'Workspace']);

        provider.dispose();
    });

    it('should include state description in tooltip for enabled features', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');
        const enabledItem = provider.getTreeItem(features[0]);
        expect.soft(enabledItem.tooltip).toContain('Enabled');

        provider.dispose();
    });

    it('should include state description in tooltip for disabled features', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        currentConfig = { mcpTools: { check_csharp_all: { disabledFeatures: ['check_csharp_async_patterns'] } } };

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');
        const disabled = features.find(c => c.kind === 'mcpToolFeatureNode' && c.state === TreeViewNodeState.Disabled)!;

        expect.soft(provider.getTreeItem(disabled).tooltip).toContain('Disabled');

        provider.dispose();
    });

    it('should include state description in tooltip for not-detected features', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');
        const notDetected = features.find(c => c.kind === 'mcpToolFeatureNode' && c.state === TreeViewNodeState.NotDetected)!;

        expect.soft(provider.getTreeItem(notDetected).tooltip).toContain('Not detected');

        provider.dispose();
    });

    it('should include setting ID in feature tooltip', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');
        const item = provider.getTreeItem(features[0]);

        expect.soft(item.tooltip).toContain('autocontext.mcpTools.');

        provider.dispose();
    });

    it('should include description and version in feature tooltip when metadata is provided', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const metadata = new Map([
            ['check_csharp_async_patterns', { description: 'Detects async anti-patterns', version: '1.2.0' }],
        ]);
        const enrichedCatalog = new McpToolsCatalog(mcpTools, metadata);
        const provider = new McpToolsTreeProvider(fakeDetector, enrichedCatalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');
        const asyncFeature = features.find(f => f.kind === 'mcpToolFeatureNode' && f.entry.contextKey === 'autocontext.mcpTools.check_csharp_async_patterns')!;
        const item = provider.getTreeItem(asyncFeature);

        expect.soft(item.tooltip).toContain('Detects async anti-patterns');
        expect.soft(item.tooltip).toContain('v1.2.0');

        provider.dispose();
    });

    it('should not include version in tooltip when metadata is absent', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');
        const item = provider.getTreeItem(features[0]);

        expect.soft(item.tooltip).not.toMatch(/\bv\d/);

        provider.dispose();
    });

    it('should include feature count in parent MCP tool tooltip', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, '.NET', 'C#');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.tooltip).toContain('check_csharp_all');
        expect.soft(item.tooltip).toContain('features enabled');

        provider.dispose();
    });

    it('should include description and version in parent tool tooltip when metadata is provided', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const metadata = new Map([
            ['check_csharp_all', { description: 'Runs all C# checks', version: '0.1.0' }],
        ]);
        const enrichedCatalog = new McpToolsCatalog(mcpTools, metadata);
        const provider = new McpToolsTreeProvider(fakeDetector, enrichedCatalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, '.NET', 'C#');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.tooltip).toContain('Runs all C# checks');
        expect.soft(item.tooltip).toContain('v0.1.0');
        expect.soft(item.tooltip).toContain('features enabled');

        provider.dispose();
    });

    it('should update feature setting when handleCheckboxChange fires for a feature', async () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');
        const asyncTool = features.find(c => c.kind === 'mcpToolFeatureNode' && c.entry.contextKey === 'autocontext.mcpTools.check_csharp_async_patterns')!;

        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        const checkboxCallback = vi.mocked(treeView.onDidChangeCheckboxState).mock.calls[0][0] as (e: { items: [typeof asyncTool, TreeItemCheckboxState][] }) => void;
        checkboxCallback({ items: [[asyncTool, TreeItemCheckboxState.Unchecked]] });

        await Promise.resolve();

        expect.soft(vi.mocked(fakeConfigManager.setMcpToolEnabled)).toHaveBeenCalledWith(
            'check_csharp_all',
            'check_csharp_async_patterns',
            false,
        );

        provider.dispose();
    });

    it('should update only parent enabled flag when handleCheckboxChange fires for a parent MCP tool', async () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, '.NET', 'C#');
        const csharpTool = tools.find(r => r.kind === 'mcpToolNode' && r.toolName === 'check_csharp_all')!;
        const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');

        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const checkboxCallback = vi.mocked(treeView.onDidChangeCheckboxState).mock.calls[0][0] as (e: { items: [any, TreeItemCheckboxState][] }) => void;

        // Simulate VS Code propagation: parent + all children in the same event.
        const propagatedItems: [any, TreeItemCheckboxState][] = [
            [csharpTool, TreeItemCheckboxState.Unchecked],
            ...features.map(f => [f, TreeItemCheckboxState.Unchecked] as [any, TreeItemCheckboxState]),
        ];
        checkboxCallback({ items: propagatedItems });
        await Promise.resolve();

        // Only the parent-level enabled flag should be set.
        expect.soft(vi.mocked(fakeConfigManager.setMcpToolEnabled)).toHaveBeenCalledWith(
            'check_csharp_all',
            undefined,
            false,
        );

        // Features should NOT be cascaded — children are skipped when parent is in the batch.
        expect.soft(vi.mocked(fakeConfigManager.setMcpToolEnabled)).toHaveBeenCalledTimes(1);

        provider.dispose();
    });

    it('should update only parent enabled flag even when all features are not-detected', async () => {
        // hasTypeScript=false so TypeScript features are not detected.
        vi.mocked(fakeDetector.get).mockImplementation((key: string) => key === 'hasCSharp');

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, 'Web', 'TypeScript');

        const tsTool = tools.find(r => r.kind === 'mcpToolNode' && r.toolName === 'check_typescript_all')!;

        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        const checkboxCallback = vi.mocked(treeView.onDidChangeCheckboxState).mock.calls[0][0] as (e: { items: [typeof tsTool, TreeItemCheckboxState][] }) => void;

        checkboxCallback({ items: [[tsTool, TreeItemCheckboxState.Checked]] });
        await Promise.resolve();

        // Parent-level enabled flag should be set.
        expect.soft(vi.mocked(fakeConfigManager.setMcpToolEnabled)).toHaveBeenCalledWith(
            'check_typescript_all',
            undefined,
            true,
        );

        // Only 1 call total (just the parent).
        expect.soft(vi.mocked(fakeConfigManager.setMcpToolEnabled)).toHaveBeenCalledTimes(1);

        provider.dispose();
    });

    it('should update leaf MCP tool setting when handleCheckboxChange fires', async () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, 'Workspace', 'EditorConfig');
        const editorConfigTool = tools.find(r => r.kind === 'mcpToolNode' && r.toolName === 'get_editorconfig')!;

        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        const checkboxCallback = vi.mocked(treeView.onDidChangeCheckboxState).mock.calls[0][0] as (e: { items: [typeof editorConfigTool, TreeItemCheckboxState][] }) => void;

        checkboxCallback({ items: [[editorConfigTool, TreeItemCheckboxState.Unchecked]] });

        await Promise.resolve();

        expect.soft(vi.mocked(fakeConfigManager.setMcpToolEnabled)).toHaveBeenCalledWith(
            'get_editorconfig',
            undefined,
            false,
        );

        provider.dispose();
    });

    it('should show enabled/total count in server tooltip when all detected', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const dotnet = roots.find(r => r.kind === 'serverNode' && r.name === '.NET')!;
        const item = provider.getTreeItem(dotnet);
        const dotnetEntries = catalog.all.filter(e => e.serverLabel === '.NET');

        expect.soft(item.tooltip).toBe(`.NET\n${dotnetEntries.length}/${dotnetEntries.length} features enabled`);

        provider.dispose();
    });

    it('should show enabled/total count in server tooltip with not-detected entries', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const workspace = roots.find(r => r.kind === 'serverNode' && r.name === 'Workspace')!;
        const item = provider.getTreeItem(workspace);
        const workspaceEntries = catalog.all.filter(e => e.serverLabel === 'Workspace');
        const alwaysOn = workspaceEntries.filter(e => !e.workspaceFlags || e.workspaceFlags.length === 0).length;

        expect.soft(item.tooltip).toBe(`Workspace\n${alwaysOn}/${workspaceEntries.length} features enabled`);

        provider.dispose();
    });

    it('should show enabled/total count in category tooltip when all detected', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        currentConfig = { mcpTools: { check_csharp_all: { disabledFeatures: ['check_csharp_async_patterns'] } } };

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const dotnet = roots.find(r => r.kind === 'serverNode' && r.name === '.NET')!;
        const categories = provider.getChildren(dotnet);
        const csharp = categories.find(r => r.kind === 'categoryNode' && r.name === 'C#')!;
        const item = provider.getTreeItem(csharp);
        const csharpEntries = catalog.all.filter(e => e.category === 'C#');
        const enabled = csharpEntries.filter(e => e.contextKey !== 'autocontext.mcpTools.check_csharp_async_patterns').length;

        expect.soft(item.tooltip).toBe(`C#\n${enabled}/${csharpEntries.length} features enabled`);

        provider.dispose();
    });

    it('should show enabled/total count in category tooltip with not-detected entries', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const dotnet = roots.find(r => r.kind === 'serverNode' && r.name === '.NET')!;
        const categories = provider.getChildren(dotnet);
        const csharp = categories.find(r => r.kind === 'categoryNode' && r.name === 'C#')!;
        const item = provider.getTreeItem(csharp);
        const csharpEntries = catalog.all.filter(e => e.category === 'C#');

        expect.soft(item.tooltip).toBe(`C#\n0/${csharpEntries.length} features enabled`);

        provider.dispose();
    });

    it('should set treeView description to enabled/total count', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        currentConfig = { mcpTools: { check_csharp_all: { disabledFeatures: ['check_csharp_async_patterns'] } } };

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        const total = catalog.count;
        const enabled = catalog.all.filter(e => e.contextKey !== 'autocontext.mcpTools.check_csharp_async_patterns').length;

        expect.soft(treeView.description).toBe(`${enabled}/${total}`);

        provider.dispose();
    });

    it('should exclude not-detected entries from enabled count in description', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        const total = catalog.count;
        const alwaysOn = catalog.all.filter(e => !e.workspaceFlags || e.workspaceFlags.length === 0).length;

        expect.soft(treeView.description).toBe(`${alwaysOn}/${total}`);

        provider.dispose();
    });

    describe('health monitor icons', () => {
        it('should show green icon when server is fully healthy', () => {
            const hm = createFakeHealthMonitor({
                isServerHealthy: () => true,
                isServerPartiallyHealthy: () => true,
            });

            const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel, hm);
            const roots = provider.getChildren();
            const dotnet = roots.find(r => r.kind === 'serverNode' && r.name === '.NET')!;
            const item = provider.getTreeItem(dotnet);

            expect.soft(item.iconPath).toBeInstanceOf(ThemeIcon);
            const icon = item.iconPath as InstanceType<typeof ThemeIcon>;
            expect.soft(icon.id).toBe('circle-filled');
            expect.soft(icon.color).toBeInstanceOf(ThemeColor);
            expect.soft((icon.color as InstanceType<typeof ThemeColor>).id).toBe('testing.iconPassed');

            provider.dispose();
        });

        it('should show yellow icon when server is partially healthy', () => {
            const hm = createFakeHealthMonitor({
                isServerHealthy: () => false,
                isServerPartiallyHealthy: () => true,
            });

            const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel, hm);
            const roots = provider.getChildren();
            const dotnet = roots.find(r => r.kind === 'serverNode' && r.name === '.NET')!;
            const item = provider.getTreeItem(dotnet);

            expect.soft(item.iconPath).toBeInstanceOf(ThemeIcon);
            const icon = item.iconPath as InstanceType<typeof ThemeIcon>;
            expect.soft(icon.id).toBe('circle-filled');
            expect.soft(icon.color).toBeInstanceOf(ThemeColor);
            expect.soft((icon.color as InstanceType<typeof ThemeColor>).id).toBe('testing.iconQueued');

            provider.dispose();
        });

        it('should show red icon when server is not healthy', () => {
            const hm = createFakeHealthMonitor({
                isServerHealthy: () => false,
                isServerPartiallyHealthy: () => false,
            });

            const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel, hm);
            const roots = provider.getChildren();
            const dotnet = roots.find(r => r.kind === 'serverNode' && r.name === '.NET')!;
            const item = provider.getTreeItem(dotnet);

            expect.soft(item.iconPath).toBeInstanceOf(ThemeIcon);
            const icon = item.iconPath as InstanceType<typeof ThemeIcon>;
            expect.soft(icon.id).toBe('circle-filled');
            expect.soft(icon.color).toBeInstanceOf(ThemeColor);
            expect.soft((icon.color as InstanceType<typeof ThemeColor>).id).toBe('testing.iconFailed');

            provider.dispose();
        });

        it('should not set iconPath when no health monitor is provided', () => {
            const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
            const roots = provider.getChildren();
            const dotnet = roots.find(r => r.kind === 'serverNode' && r.name === '.NET')!;
            const item = provider.getTreeItem(dotnet);

            expect.soft(item.iconPath).toBeUndefined();

            provider.dispose();
        });

        it('should subscribe to onDidChange and refresh on health change', () => {
            const hm = createFakeHealthMonitor();
            const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel, hm);

            expect.soft(hm.onDidChange).toHaveBeenCalledOnce();

            provider.dispose();
        });
    });

    describe('server status (gray) icons', () => {
        function createFakeServerProvider(statusFn: (serverLabel: string) => 'unavailable' | 'disabled' | 'available') {
            return { getServerStatus: vi.fn(statusFn) } as unknown as import('../../src/mcp-server-provider').McpServerProvider;
        }

        it('should show gray icon when server status is unavailable', () => {
            const sp = createFakeServerProvider(() => 'unavailable');
            const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel, undefined, sp);
            const dotnet = provider.getChildren().find(r => r.kind === 'serverNode' && r.name === '.NET')!;
            const item = provider.getTreeItem(dotnet);

            expect.soft(item.iconPath).toBeInstanceOf(ThemeIcon);
            const icon = item.iconPath as InstanceType<typeof ThemeIcon>;
            expect.soft(icon.id).toBe('circle-filled');
            expect.soft((icon.color as InstanceType<typeof ThemeColor>).id).toBe('disabledForeground');

            provider.dispose();
        });

        it('should show gray icon when server status is disabled', () => {
            const sp = createFakeServerProvider(() => 'disabled');
            const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel, undefined, sp);
            const dotnet = provider.getChildren().find(r => r.kind === 'serverNode' && r.name === '.NET')!;
            const item = provider.getTreeItem(dotnet);

            expect.soft(item.iconPath).toBeInstanceOf(ThemeIcon);
            const icon = item.iconPath as InstanceType<typeof ThemeIcon>;
            expect.soft(icon.id).toBe('circle-filled');
            expect.soft((icon.color as InstanceType<typeof ThemeColor>).id).toBe('disabledForeground');

            provider.dispose();
        });

        it('should append "Not detected" to tooltip when unavailable', () => {
            const sp = createFakeServerProvider(() => 'unavailable');
            const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel, undefined, sp);
            const dotnet = provider.getChildren().find(r => r.kind === 'serverNode' && r.name === '.NET')!;
            const item = provider.getTreeItem(dotnet);

            expect.soft(item.tooltip as string).toContain('Not detected');

            provider.dispose();
        });

        it('should append "Not active in this workspace" to tooltip when disabled', () => {
            const sp = createFakeServerProvider(() => 'disabled');
            const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel, undefined, sp);
            const dotnet = provider.getChildren().find(r => r.kind === 'serverNode' && r.name === '.NET')!;
            const item = provider.getTreeItem(dotnet);

            expect.soft(item.tooltip as string).toContain('Not active in this workspace');

            provider.dispose();
        });

        it('should fall through to health monitor icons when status is available', () => {
            const sp = createFakeServerProvider(() => 'available');
            const hm = createFakeHealthMonitor({ isServerHealthy: () => true, isServerPartiallyHealthy: () => true });
            const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel, hm, sp);
            const dotnet = provider.getChildren().find(r => r.kind === 'serverNode' && r.name === '.NET')!;
            const item = provider.getTreeItem(dotnet);

            const icon = item.iconPath as InstanceType<typeof ThemeIcon>;
            expect.soft((icon.color as InstanceType<typeof ThemeColor>).id).toBe('testing.iconPassed');

            provider.dispose();
        });

        it('should show no icon when no serverProvider and no healthMonitor', () => {
            const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, outputChannel);
            const dotnet = provider.getChildren().find(r => r.kind === 'serverNode' && r.name === '.NET')!;
            const item = provider.getTreeItem(dotnet);

            expect.soft(item.iconPath).toBeUndefined();

            provider.dispose();
        });
    });

    describe('error logging', () => {
        it('should log to outputChannel when configManager.read rejects in onDidChange', async () => {
            let onDidChangeCallback!: () => void;
            const failingConfigManager = {
                readSync: vi.fn(() => ({})),
                read: vi.fn().mockRejectedValue(new Error('read boom')),
                onDidChange: vi.fn((cb: () => void) => { onDidChangeCallback = cb; return { dispose: vi.fn() }; }),
            } as unknown as import('../../src/autocontext-config').AutoContextConfigManager;

            const oc = createFakeOutputChannel();
            const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, failingConfigManager, oc);

            onDidChangeCallback();
            await vi.waitFor(() => {
                expect(oc.appendLine).toHaveBeenCalledWith(
                    expect.stringContaining('[McpToolsTree] Failed to update config: read boom'),
                );
            });

            provider.dispose();
        });

        it('should log to outputChannel when handleCheckboxChange rejects', async () => {
            vi.mocked(fakeDetector.get).mockReturnValue(true);
            vi.mocked(fakeConfigManager.setMcpToolEnabled).mockRejectedValueOnce(new Error('write boom'));

            const oc = createFakeOutputChannel();
            const provider = new McpToolsTreeProvider(fakeDetector, catalog, stateResolver, tooltip, fakeConfigManager, oc);
            const features = getFeatures(provider, '.NET', 'C#', 'check_csharp_all');
            const feature = features.find(c => c.kind === 'mcpToolFeatureNode')!;

            const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
            const checkboxCallback = vi.mocked(treeView.onDidChangeCheckboxState).mock.calls[0][0] as
                (e: { items: [typeof feature, TreeItemCheckboxState][] }) => void;

            checkboxCallback({ items: [[feature, TreeItemCheckboxState.Unchecked]] });
            await vi.waitFor(() => {
                expect(oc.appendLine).toHaveBeenCalledWith(
                    expect.stringContaining('[McpToolsTree] Failed to handle checkbox change: write boom'),
                );
            });

            provider.dispose();
        });
    });
});
