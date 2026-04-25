import { describe, it, expect, vi, beforeEach } from 'vitest';
import { join } from 'node:path';
import { TreeItemCollapsibleState, TreeItemCheckboxState, window, ThemeIcon, ThemeColor } from './_fakes/fake-vscode';
import { McpToolsTreeProvider } from '../../src/mcp-tools-tree-provider';
import type { AutoContextConfig } from '../../src/types/autocontext-config';
import { TreeViewNodeState } from '../../src/tree-view-node-state';
import { McpToolsCatalog } from '../../src/mcp-tools-catalog';
import { McpToolsManifestLoader } from '../../src/mcp-tools-manifest-loader';
import { TreeViewStateResolver } from '../../src/tree-view-state-resolver';
import { TreeViewTooltip } from '../../src/tree-view-tooltip';
import { createFakeDetector, createFakeConfigManager, createFakeHealthMonitor, createFakeOutputChannel } from './_fakes';

const manifest = new McpToolsManifestLoader(join(__dirname, '..', '..')).load();

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
    const catalog = new McpToolsCatalog(manifest);

    /** Navigate: root → serverLabel → category → MCP tools */
    function getMcpTools(provider: McpToolsTreeProvider, serverLabelName: string, categoryName: string) {
        const serverLabels = provider.getChildren();
        const serverLabel = serverLabels.find(r => r.kind === 'serverNode' && r.name === serverLabelName)!;
        const categories = provider.getChildren(serverLabel);
        const category = categories.find(r => r.kind === 'categoryNode' && r.name === categoryName)!;
        return provider.getChildren(category);
    }

    /** Navigate: root → serverLabel → category → MCP tool → tasks */
    function getTasks(provider: McpToolsTreeProvider, serverLabelName: string, categoryName: string, toolName: string) {
        const tools = getMcpTools(provider, serverLabelName, categoryName);
        const tool = tools.find(r => r.kind === 'mcpToolNode' && r.tool.name === toolName)!;
        return provider.getChildren(tool);
    }

    it('should return server nodes as root elements', () => {
        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();

        const names = roots.map(r => r.kind === 'serverNode' ? r.name : '');
        expect.soft(names).toEqual(['.NET', 'Web', 'Workspace']);

        provider.dispose();
    });

    it('should return category nodes as children of a server node', () => {
        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const dotnet = roots.find(r => r.kind === 'serverNode' && r.name === '.NET')!;
        const categories = provider.getChildren(dotnet);

        const names = categories.map(r => r.kind === 'categoryNode' ? r.name : '');
        expect.soft(names).toEqual(['C#', 'NuGet']);

        provider.dispose();
    });

    it('should return MCP tool nodes as children of a category', () => {
        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, '.NET', 'C#');

        expect.soft(tools.length).toBe(1);
        expect.soft(tools[0].kind).toBe('mcpToolNode');
        expect.soft(tools[0].kind === 'mcpToolNode' && tools[0].tool.name).toBe('analyze_csharp_code');

        provider.dispose();
    });

    it('should return task nodes as children of a parent MCP tool', () => {
        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tasks = getTasks(provider, '.NET', 'C#', 'analyze_csharp_code');

        expect.soft(tasks.length).toBeGreaterThan(0);
        expect.soft(tasks.every(c => c.kind === 'mcpTaskNode')).toBe(true);

        provider.dispose();
    });

    it('should return empty array for task node children', () => {
        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tasks = getTasks(provider, '.NET', 'C#', 'analyze_csharp_code');

        expect.soft(provider.getChildren(tasks[0])).toEqual([]);

        provider.dispose();
    });

    it('should mark tasks as not-detected when context is missing', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tasks = getTasks(provider, '.NET', 'C#', 'analyze_csharp_code');

        expect.soft(tasks.every(c => c.kind === 'mcpTaskNode' && c.state === TreeViewNodeState.NotDetected)).toBe(true);

        provider.dispose();
    });

    it('should mark tasks as enabled when context is detected and setting is true', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tasks = getTasks(provider, '.NET', 'C#', 'analyze_csharp_code');

        expect.soft(tasks.every(c => c.kind === 'mcpTaskNode' && c.state === TreeViewNodeState.Enabled)).toBe(true);

        provider.dispose();
    });

    it('should mark tasks as disabled when setting is false', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        currentConfig = { mcpTools: { analyze_csharp_code: { disabledTasks: ['analyze_csharp_async_patterns'] } } };

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tasks = getTasks(provider, '.NET', 'C#', 'analyze_csharp_code');

        const asyncTool = tasks.find(c => c.kind === 'mcpTaskNode' && c.task.name === 'analyze_csharp_async_patterns');
        expect.soft(asyncTool?.kind === 'mcpTaskNode' && asyncTool.state).toBe(TreeViewNodeState.Disabled);

        provider.dispose();
    });

    it('should show checkbox checked for enabled tasks', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tasks = getTasks(provider, '.NET', 'C#', 'analyze_csharp_code');
        const item = provider.getTreeItem(tasks[0]);

        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Checked);

        provider.dispose();
    });

    it('should show checkbox unchecked for disabled tasks', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        currentConfig = { mcpTools: { analyze_csharp_code: { disabledTasks: ['analyze_csharp_async_patterns'] } } };

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tasks = getTasks(provider, '.NET', 'C#', 'analyze_csharp_code');
        const asyncTool = tasks.find(c => c.kind === 'mcpTaskNode' && c.task.name === 'analyze_csharp_async_patterns')!;
        const item = provider.getTreeItem(asyncTool);

        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Unchecked);

        provider.dispose();
    });

    it('should not show checkbox for not-detected tasks', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tasks = getTasks(provider, '.NET', 'C#', 'analyze_csharp_code');
        const item = provider.getTreeItem(tasks[0]);

        expect.soft(item.checkboxState).toBeUndefined();
        expect.soft(item.description).toBe('not detected');

        provider.dispose();
    });

    it('should show server items as expanded with contextValue', () => {
        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const item = provider.getTreeItem(roots[0]);

        expect.soft(item.collapsibleState).toBe(TreeItemCollapsibleState.Expanded);
        expect.soft(item.contextValue).toBe('serverNode.stopped');

        provider.dispose();
    });

    it('should show category items as expanded with contextValue and no checkbox', () => {
        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const dotnet = roots.find(r => r.kind === 'serverNode' && r.name === '.NET')!;
        const categories = provider.getChildren(dotnet);
        const item = provider.getTreeItem(categories[0]);

        expect.soft(item.collapsibleState).toBe(TreeItemCollapsibleState.Expanded);
        expect.soft(item.contextValue).toBe('categoryNode');
        expect.soft(item.checkboxState).toBeUndefined();

        provider.dispose();
    });

    it('should show MCP tool checkbox checked when all tasks are enabled', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, '.NET', 'C#');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Checked);

        provider.dispose();
    });

    it('should show MCP tool checkbox checked when some tasks are disabled', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        currentConfig = { mcpTools: { analyze_csharp_code: { disabledTasks: ['analyze_csharp_async_patterns'] } } };

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, '.NET', 'C#');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Checked);

        provider.dispose();
    });

    it('should show MCP tool checkbox checked when all tasks are disabled but parent is not', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        const allTasks = catalog.all
            .filter(e => e.toolName === 'analyze_csharp_code' && e.taskName)
            .map(e => e.taskName!);
        currentConfig = { mcpTools: { analyze_csharp_code: { disabledTasks: allTasks } } };

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, '.NET', 'C#');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Checked);

        provider.dispose();
    });

    it('should show MCP tool checkbox unchecked when parent has enabled:false', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        currentConfig = { mcpTools: { analyze_csharp_code: { enabled: false } } };

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, '.NET', 'C#');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Unchecked);

        provider.dispose();
    });

    it('should show MCP tool checkbox unchecked when parent entry is false', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        currentConfig = { mcpTools: { analyze_csharp_code: false } };

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, '.NET', 'C#');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Unchecked);

        provider.dispose();
    });

    it('should show MCP tool checkbox undefined when all tasks are not-detected', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, '.NET', 'C#');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.checkboxState).toBeUndefined();

        provider.dispose();
    });

    it('should show parent MCP tool as expanded with contextValue', () => {
        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, '.NET', 'C#');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.collapsibleState).toBe(TreeItemCollapsibleState.Expanded);
        expect.soft(item.contextValue).toBe('mcpToolNode');

        provider.dispose();
    });

    it('should show leaf MCP tool checkbox checked when enabled', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, 'Workspace', 'EditorConfig');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Checked);

        provider.dispose();
    });

    it('should sort enabled before disabled before not-detected in tasks', () => {
        vi.mocked(fakeDetector.get).mockImplementation((key: string) => key === 'hasCSharp');
        currentConfig = { mcpTools: { analyze_csharp_code: { disabledTasks: ['analyze_csharp_async_patterns'] } } };

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tasks = getTasks(provider, '.NET', 'C#', 'analyze_csharp_code');

        const states = tasks.map(c => c.kind === 'mcpTaskNode' ? c.state : '');
        const enabledIdx = states.indexOf(TreeViewNodeState.Enabled);
        const disabledIdx = states.indexOf(TreeViewNodeState.Disabled);

        expect.soft(enabledIdx).toBeLessThan(disabledIdx);

        provider.dispose();
    });

    it('should include tools without context keys as enabled by default', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, 'Workspace', 'EditorConfig');

        expect.soft(tools.length).toBe(1);
        const item = provider.getTreeItem(tools[0]);
        expect.soft(item.checkboxState).toBe(TreeItemCheckboxState.Checked);

        provider.dispose();
    });

    it('should hide not-detected tools when showNotDetected is false', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        provider.showNotDetected = false;

        const roots = provider.getChildren();
        // Only Workspace server should remain (EditorConfig has no context keys)
        const names = roots.map(r => r.kind === 'serverNode' ? r.name : '');
        expect.soft(names).toEqual(['Workspace']);

        provider.dispose();
    });

    it('should show not-detected tools when showNotDetected is true', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        provider.showNotDetected = true;

        const roots = provider.getChildren();
        const names = roots.map(r => r.kind === 'serverNode' ? r.name : '');
        expect.soft(names).toEqual(['.NET', 'Web', 'Workspace']);

        provider.dispose();
    });

    it('should include state description in tooltip for enabled tasks', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tasks = getTasks(provider, '.NET', 'C#', 'analyze_csharp_code');
        const enabledItem = provider.getTreeItem(tasks[0]);
        expect.soft(enabledItem.tooltip).toContain('Enabled');

        provider.dispose();
    });

    it('should include state description in tooltip for disabled tasks', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        currentConfig = { mcpTools: { analyze_csharp_code: { disabledTasks: ['analyze_csharp_async_patterns'] } } };

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tasks = getTasks(provider, '.NET', 'C#', 'analyze_csharp_code');
        const disabled = tasks.find(c => c.kind === 'mcpTaskNode' && c.state === TreeViewNodeState.Disabled)!;

        expect.soft(provider.getTreeItem(disabled).tooltip).toContain('Disabled');

        provider.dispose();
    });

    it('should include state description in tooltip for not-detected tasks', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tasks = getTasks(provider, '.NET', 'C#', 'analyze_csharp_code');
        const notDetected = tasks.find(c => c.kind === 'mcpTaskNode' && c.state === TreeViewNodeState.NotDetected)!;

        expect.soft(provider.getTreeItem(notDetected).tooltip).toContain('Not detected');

        provider.dispose();
    });

    it('should include setting ID in task tooltip', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tasks = getTasks(provider, '.NET', 'C#', 'analyze_csharp_code');
        const item = provider.getTreeItem(tasks[0]);

        expect.soft(item.tooltip).toContain('autocontext.mcpTools.');

        provider.dispose();
    });

    it('should include description in task tooltip when descriptions are provided', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tasks = getTasks(provider, '.NET', 'C#', 'analyze_csharp_code');
        const asyncTask = tasks.find(f => f.kind === 'mcpTaskNode' && f.task.name === 'analyze_csharp_async_patterns')!;
        const item = provider.getTreeItem(asyncTask);

        // Description comes from the manifest JSON, exposed on the entry.
        const expected = manifest.toolByName('analyze_csharp_code')!
            .tasks.find(t => t.name === 'analyze_csharp_async_patterns')!.description!;
        expect.soft(item.tooltip).toContain(expected);

        provider.dispose();
    });

    it('should not include version in tooltip when metadata is absent', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tasks = getTasks(provider, '.NET', 'C#', 'analyze_csharp_code');
        const item = provider.getTreeItem(tasks[0]);

        expect.soft(item.tooltip).not.toMatch(/\bv\d/);

        provider.dispose();
    });

    it('should include task count in parent MCP tool tooltip', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, '.NET', 'C#');
        const item = provider.getTreeItem(tools[0]);

        expect.soft(item.tooltip).toContain('analyze_csharp_code');
        expect.soft(item.tooltip).toContain('tasks enabled');

        provider.dispose();
    });

    it('should include description in parent tool tooltip when descriptions are provided', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, '.NET', 'C#');
        const item = provider.getTreeItem(tools[0]);

        // Description comes from the manifest JSON, exposed on the tool entry.
        const expected = manifest.toolByName('analyze_csharp_code')!.description!;
        expect.soft(item.tooltip).toContain(expected);
        expect.soft(item.tooltip).toContain('tasks enabled');

        provider.dispose();
    });

    it('should update task setting when handleCheckboxChange fires for a task', async () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tasks = getTasks(provider, '.NET', 'C#', 'analyze_csharp_code');
        const asyncTool = tasks.find(c => c.kind === 'mcpTaskNode' && c.task.name === 'analyze_csharp_async_patterns')!;

        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        const checkboxCallback = vi.mocked(treeView.onDidChangeCheckboxState).mock.calls[0][0] as (e: { items: [typeof asyncTool, TreeItemCheckboxState][] }) => void;
        checkboxCallback({ items: [[asyncTool, TreeItemCheckboxState.Unchecked]] });

        await Promise.resolve();

        expect.soft(vi.mocked(fakeConfigManager.setMcpToolEnabled)).toHaveBeenCalledWith(
            'analyze_csharp_code',
            'analyze_csharp_async_patterns',
            false,
        );

        provider.dispose();
    });

    it('should update only parent enabled flag when handleCheckboxChange fires for a parent MCP tool', async () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, '.NET', 'C#');
        const csharpTool = tools.find(r => r.kind === 'mcpToolNode' && r.tool.name === 'analyze_csharp_code')!;
        const tasks = getTasks(provider, '.NET', 'C#', 'analyze_csharp_code');

        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const checkboxCallback = vi.mocked(treeView.onDidChangeCheckboxState).mock.calls[0][0] as (e: { items: [any, TreeItemCheckboxState][] }) => void;

        // Simulate VS Code propagation: parent + all children in the same event.
        const propagatedItems: [any, TreeItemCheckboxState][] = [
            [csharpTool, TreeItemCheckboxState.Unchecked],
            ...tasks.map(f => [f, TreeItemCheckboxState.Unchecked] as [any, TreeItemCheckboxState]),
        ];
        checkboxCallback({ items: propagatedItems });
        await Promise.resolve();

        // Only the parent-level enabled flag should be set.
        expect.soft(vi.mocked(fakeConfigManager.setMcpToolEnabled)).toHaveBeenCalledWith(
            'analyze_csharp_code',
            undefined,
            false,
        );

        // tasks should NOT be cascaded — children are skipped when parent is in the batch.
        expect.soft(vi.mocked(fakeConfigManager.setMcpToolEnabled)).toHaveBeenCalledTimes(1);

        provider.dispose();
    });

    it('should update only parent enabled flag even when all tasks are not-detected', async () => {
        // hasTypeScript=false so TypeScript tasks are not detected.
        vi.mocked(fakeDetector.get).mockImplementation((key: string) => key === 'hasCSharp');

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const tools = getMcpTools(provider, 'Web', 'TypeScript');

        const tsTool = tools.find(r => r.kind === 'mcpToolNode' && r.tool.name === 'analyze_typescript_code')!;

        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        const checkboxCallback = vi.mocked(treeView.onDidChangeCheckboxState).mock.calls[0][0] as (e: { items: [typeof tsTool, TreeItemCheckboxState][] }) => void;

        checkboxCallback({ items: [[tsTool, TreeItemCheckboxState.Checked]] });
        await Promise.resolve();

        // Parent-level enabled flag should be set.
        expect.soft(vi.mocked(fakeConfigManager.setMcpToolEnabled)).toHaveBeenCalledWith(
            'analyze_typescript_code',
            undefined,
            true,
        );

        // Only 1 call total (just the parent).
        expect.soft(vi.mocked(fakeConfigManager.setMcpToolEnabled)).toHaveBeenCalledTimes(1);

        provider.dispose();
    });

    it('should show enabled/total count in server tooltip when all detected', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const dotnet = roots.find(r => r.kind === 'serverNode' && r.name === '.NET')!;
        const item = provider.getTreeItem(dotnet);
        const dotnetEntries = catalog.all.filter(e => e.workerCategory.name === '.NET');

        expect.soft(item.tooltip).toBe(`.NET\n${dotnetEntries.length}/${dotnetEntries.length} tasks enabled`);

        provider.dispose();
    });

    it('should show enabled/total count in server tooltip with not-detected entries', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const workspace = roots.find(r => r.kind === 'serverNode' && r.name === 'Workspace')!;
        const item = provider.getTreeItem(workspace);
        const workspaceEntries = catalog.all.filter(e => e.workerCategory.name === 'Workspace');
        const alwaysOn = workspaceEntries.filter(e => !e.workspaceFlags || e.workspaceFlags.length === 0).length;

        expect.soft(item.tooltip).toBe(`Workspace\n${alwaysOn}/${workspaceEntries.length} tasks enabled`);

        provider.dispose();
    });

    it('should show enabled/total count in category tooltip when all detected', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        currentConfig = { mcpTools: { analyze_csharp_code: { disabledTasks: ['analyze_csharp_async_patterns'] } } };

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const dotnet = roots.find(r => r.kind === 'serverNode' && r.name === '.NET')!;
        const categories = provider.getChildren(dotnet);
        const csharp = categories.find(r => r.kind === 'categoryNode' && r.name === 'C#')!;
        const item = provider.getTreeItem(csharp);
        const csharpEntries = catalog.all.filter(e => e.leafCategory.name === 'C#');
        const enabled = csharpEntries.filter(e => e.contextKey !== 'autocontext.mcpTools.analyze_csharp_async_patterns').length;

        expect.soft(item.tooltip).toBe(`C#\n${enabled}/${csharpEntries.length} tasks enabled`);

        provider.dispose();
    });

    it('should show enabled/total count in category tooltip with not-detected entries', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const roots = provider.getChildren();
        const dotnet = roots.find(r => r.kind === 'serverNode' && r.name === '.NET')!;
        const categories = provider.getChildren(dotnet);
        const csharp = categories.find(r => r.kind === 'categoryNode' && r.name === 'C#')!;
        const item = provider.getTreeItem(csharp);
        const csharpEntries = catalog.all.filter(e => e.leafCategory.name === 'C#');

        expect.soft(item.tooltip).toBe(`C#\n0/${csharpEntries.length} tasks enabled`);

        provider.dispose();
    });

    it('should set treeView description to enabled/total count', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(true);
        currentConfig = { mcpTools: { analyze_csharp_code: { disabledTasks: ['analyze_csharp_async_patterns'] } } };

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        const total = catalog.count;
        const enabled = catalog.all.filter(e => e.contextKey !== 'autocontext.mcpTools.analyze_csharp_async_patterns').length;

        expect.soft(treeView.description).toBe(`${enabled}/${total}`);

        provider.dispose();
    });

    it('should exclude not-detected entries from enabled count in description', () => {
        vi.mocked(fakeDetector.get).mockReturnValue(false);

        const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
        const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
        const total = catalog.count;
        const alwaysOn = catalog.all.filter(e => !e.workspaceFlags || e.workspaceFlags.length === 0).length;

        expect.soft(treeView.description).toBe(`${alwaysOn}/${total}`);

        provider.dispose();
    });

    describe('health monitor icons', () => {
        it('should show green icon when server label is running', () => {
            const hm = createFakeHealthMonitor({ isRunningServerLabel: () => true });

            const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel, hm);
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

        it('should show red icon when server label is not running', () => {
            const hm = createFakeHealthMonitor({ isRunningServerLabel: () => false });

            const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel, hm);
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
            const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
            const roots = provider.getChildren();
            const dotnet = roots.find(r => r.kind === 'serverNode' && r.name === '.NET')!;
            const item = provider.getTreeItem(dotnet);

            expect.soft(item.iconPath).toBeUndefined();

            provider.dispose();
        });

        it('should subscribe to onDidChange and refresh on health change', () => {
            const hm = createFakeHealthMonitor();
            const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel, hm);

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
            const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel, undefined, sp);
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
            const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel, undefined, sp);
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
            const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel, undefined, sp);
            const dotnet = provider.getChildren().find(r => r.kind === 'serverNode' && r.name === '.NET')!;
            const item = provider.getTreeItem(dotnet);

            expect.soft(item.tooltip as string).toContain('Not detected');

            provider.dispose();
        });

        it('should append "Not active in this workspace" to tooltip when disabled', () => {
            const sp = createFakeServerProvider(() => 'disabled');
            const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel, undefined, sp);
            const dotnet = provider.getChildren().find(r => r.kind === 'serverNode' && r.name === '.NET')!;
            const item = provider.getTreeItem(dotnet);

            expect.soft(item.tooltip as string).toContain('Not active in this workspace');

            provider.dispose();
        });

        it('should fall through to health monitor icons when status is available', () => {
            const sp = createFakeServerProvider(() => 'available');
            const hm = createFakeHealthMonitor({ isRunningServerLabel: () => true });
            const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel, hm, sp);
            const dotnet = provider.getChildren().find(r => r.kind === 'serverNode' && r.name === '.NET')!;
            const item = provider.getTreeItem(dotnet);

            const icon = item.iconPath as InstanceType<typeof ThemeIcon>;
            expect.soft((icon.color as InstanceType<typeof ThemeColor>).id).toBe('testing.iconPassed');

            provider.dispose();
        });

        it('should show no icon when no serverProvider and no healthMonitor', () => {
            const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, outputChannel);
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
            const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, failingConfigManager, oc);

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
            const provider = new McpToolsTreeProvider(fakeDetector, manifest, stateResolver, tooltip, fakeConfigManager, oc);
            const tasks = getTasks(provider, '.NET', 'C#', 'analyze_csharp_code');
            const task = tasks.find(c => c.kind === 'mcpTaskNode')!;

            const treeView = vi.mocked(window.createTreeView).mock.results.at(-1)!.value;
            const checkboxCallback = vi.mocked(treeView.onDidChangeCheckboxState).mock.calls[0][0] as
                (e: { items: [typeof task, TreeItemCheckboxState][] }) => void;

            checkboxCallback({ items: [[task, TreeItemCheckboxState.Unchecked]] });
            await vi.waitFor(() => {
                expect(oc.appendLine).toHaveBeenCalledWith(
                    expect.stringContaining('[McpToolsTree] Failed to handle checkbox change: write boom'),
                );
            });

            provider.dispose();
        });
    });
});
