import { describe, it, expect, vi, beforeEach } from 'vitest';
import { workspace, McpStdioServerDefinition, __setConfigStore } from './__mocks__/vscode';
import { McpServerProvider } from '../../src/mcp-server-provider';
import { McpToolsCatalog } from '../../src/mcp-tools-catalog';
import { McpServersCatalog } from '../../src/mcp-servers-catalog';
import { mcpTools, mcpServers } from '../../src/ui-constants';
import type { WorkspaceContextDetector } from '../../src/workspace-context-detector';
import type { WorkspaceServerManager } from '../../src/workspace-server-manager';
import type { HealthMonitorServer } from '../../src/health-monitor';

const { existsSyncMock } = vi.hoisted(() => ({ existsSyncMock: vi.fn<(path: string) => boolean>(() => true) }));
vi.mock('node:fs', () => ({ existsSync: existsSyncMock }));

type StdioDef = InstanceType<typeof McpStdioServerDefinition>;

const extensionPath = '/ext';
const version = '1.0.0';

const fakeDetector = {
    get: vi.fn((_key: string) => true),
} as unknown as WorkspaceContextDetector;

const fakeWorkspaceServer = {
    getPipeName: vi.fn(() => 'autocontext-workspace-abc123'),
} as unknown as WorkspaceServerManager;

const fakeHealthMonitor = {
    getPipeName: vi.fn(() => 'autocontext-health-abc123'),
} as unknown as HealthMonitorServer;

const onDidChange = vi.fn() as unknown as import('vscode').Event<void>;
const toolsCatalog = new McpToolsCatalog(mcpTools);
const serversCatalog = new McpServersCatalog(mcpServers);

function createProvider(): McpServerProvider {
    return new McpServerProvider(extensionPath, version, fakeDetector, onDidChange, fakeWorkspaceServer, toolsCatalog, serversCatalog, fakeHealthMonitor);
}

beforeEach(() => {
    vi.clearAllMocks();
    __setConfigStore({});
    existsSyncMock.mockReturnValue(true);
    vi.mocked(fakeDetector.get).mockReturnValue(true);
    vi.mocked(fakeWorkspaceServer.getPipeName).mockReturnValue('autocontext-workspace-abc123');
    vi.mocked(fakeHealthMonitor.getPipeName).mockReturnValue('autocontext-health-abc123');
    workspace.workspaceFolders = [{ uri: { fsPath: '/workspace' } }];
});

describe('McpServerProvider.provideMcpServerDefinitions', () => {
    describe('process-to-binary mapping', () => {
        it('dotnet process resolves to AutoContext.Mcp.DotNet binary', async () => {
            const defs = await createProvider().provideMcpServerDefinitions() as StdioDef[];
            const dotnet = defs.find(d => d.args?.includes('dotnet'));

            expect(dotnet).toBeDefined();
            expect.soft(dotnet!.command).toContain('AutoContext.Mcp.DotNet');
        });

        it('workspace process resolves to AutoContext.WorkspaceServer binary', async () => {
            const defs = await createProvider().provideMcpServerDefinitions() as StdioDef[];
            const git = defs.find(d => d.args?.includes('git'));
            const editorconfig = defs.find(d => d.args?.includes('editorconfig'));

            expect.soft(git).toBeDefined();
            expect.soft(git?.command).toContain('AutoContext.WorkspaceServer');
            expect.soft(editorconfig).toBeDefined();
            expect.soft(editorconfig?.command).toContain('AutoContext.WorkspaceServer');
        });

        it('web process resolves to node with index.js', async () => {
            const defs = await createProvider().provideMcpServerDefinitions() as StdioDef[];
            const ts = defs.find(d => d.args?.includes('typescript'));

            expect(ts).toBeDefined();
            expect(ts!.command).toBe('node');
            expect.soft(ts!.args).toEqual(expect.arrayContaining([
                expect.stringContaining('AutoContext.Mcp.Web'),
            ]));
        });
    });

    describe('argument assembly', () => {
        it('every server receives --scope with its category', async () => {
            const defs = await createProvider().provideMcpServerDefinitions() as StdioDef[];

            expect(defs).not.toHaveLength(0);
            expect.soft(defs.every(d => {
                const idx = d.args!.indexOf('--scope');
                return idx >= 0 && !!d.args![idx + 1];
            })).toBe(true);
        });

        it('every server receives --workspace-folder when a folder is open', async () => {
            const defs = await createProvider().provideMcpServerDefinitions() as StdioDef[];

            expect(defs).not.toHaveLength(0);
            expect.soft(defs.every(d =>
                d.args!.includes('--workspace-folder') && d.args!.includes('/workspace'),
            )).toBe(true);
        });

        it('--workspace-folder is omitted when no folder is open', async () => {
            workspace.workspaceFolders = undefined;

            const defs = await createProvider().provideMcpServerDefinitions() as StdioDef[];

            expect(defs).not.toHaveLength(0);
            expect.soft(defs.every(d => !d.args!.includes('--workspace-folder'))).toBe(true);
        });

        it('non-editorconfig servers receive --workspace-server', async () => {
            const defs = await createProvider().provideMcpServerDefinitions() as StdioDef[];
            const nonEditorconfig = defs.filter(d => {
                const category = d.args![d.args!.indexOf('--scope') + 1];
                return category !== 'editorconfig';
            });

            expect(nonEditorconfig).not.toHaveLength(0);
            expect.soft(nonEditorconfig.every(d =>
                d.args!.includes('--workspace-server') && d.args!.includes('autocontext-workspace-abc123'),
            )).toBe(true);
        });

        it('editorconfig server does not receive --workspace-server', async () => {
            const defs = await createProvider().provideMcpServerDefinitions() as StdioDef[];
            const editorconfig = defs.find(d => d.args?.includes('editorconfig'));

            expect.soft(editorconfig!.args).not.toEqual(expect.arrayContaining(['--workspace-server']));
        });

        it('--workspace-server is omitted when pipe is not ready', async () => {
            vi.mocked(fakeWorkspaceServer.getPipeName).mockReturnValue(undefined);

            const defs = await createProvider().provideMcpServerDefinitions() as StdioDef[];

            expect(defs).not.toHaveLength(0);
            expect.soft(defs.every(d => !d.args!.includes('--workspace-server'))).toBe(true);
        });
    });

    describe('context-based filtering', () => {
        it('server with unmet contextKey is excluded', async () => {
            vi.mocked(fakeDetector.get).mockImplementation(
                (key: string) => key !== 'hasDotNet',
            );

            const defs = await createProvider().provideMcpServerDefinitions() as StdioDef[];
            const categories = defs.map(d => d.args![d.args!.indexOf('--scope') + 1]);

            expect.soft(categories).not.toContain('dotnet');
            expect.soft(categories).toContain('git');
            expect.soft(categories).toContain('editorconfig');
            expect.soft(categories).toContain('typescript');
        });

        it('editorconfig is always included (no contextKey)', async () => {
            vi.mocked(fakeDetector.get).mockReturnValue(false);

            const defs = await createProvider().provideMcpServerDefinitions() as StdioDef[];
            const categories = defs.map(d => d.args![d.args!.indexOf('--scope') + 1]);

            expect.soft(categories).toContain('editorconfig');
        });

        it('server is excluded when all its tools are disabled', async () => {
            __setConfigStore({
                'autocontext.mcpTools.check_git_commit_content': false,
                'autocontext.mcpTools.check_git_commit_format': false,
            });

            const defs = await createProvider().provideMcpServerDefinitions() as StdioDef[];
            const categories = defs.map(d => d.args![d.args!.indexOf('--scope') + 1]);

            expect.soft(categories).not.toContain('git');
        });

        it('server is included when at least one tool is enabled', async () => {
            __setConfigStore({
                'autocontext.mcpTools.check_git_commit_content': false,
            });

            const defs = await createProvider().provideMcpServerDefinitions() as StdioDef[];
            const categories = defs.map(d => d.args![d.args!.indexOf('--scope') + 1]);

            expect.soft(categories).toContain('git');
        });
    });

    describe('definition shape', () => {
        it('all definitions are McpStdioServerDefinition instances', async () => {
            const defs = await createProvider().provideMcpServerDefinitions();

            expect(defs).not.toHaveLength(0);
            expect.soft(defs.every(d => d instanceof McpStdioServerDefinition)).toBe(true);
        });

        it('all definitions carry the extension version', async () => {
            const defs = await createProvider().provideMcpServerDefinitions();

            expect(defs).not.toHaveLength(0);
            expect.soft(defs.every(d =>
                (d as InstanceType<typeof McpStdioServerDefinition>).version === version,
            )).toBe(true);
        });

        it('returns four servers when all contexts are met', async () => {
            const defs = await createProvider().provideMcpServerDefinitions();

            expect.soft(defs).toHaveLength(4);
        });
    });

    describe('health monitor argument', () => {
        it('every server receives --health-monitor with the pipe name', async () => {
            const defs = await createProvider().provideMcpServerDefinitions() as StdioDef[];

            expect(defs).not.toHaveLength(0);
            expect.soft(defs.every(d =>
                d.args!.includes('--health-monitor') && d.args!.includes('autocontext-health-abc123'),
            )).toBe(true);
        });
    });
});

describe('McpServerProvider.getServerStatus', () => {
    it('returns available when binary exists and context is detected', () => {
        expect(createProvider().getServerStatus('.NET')).toBe('available');
    });

    it('returns unavailable when binary does not exist', () => {
        existsSyncMock.mockReturnValue(false);

        expect(createProvider().getServerStatus('.NET')).toBe('unavailable');
    });

    it('returns disabled when the context key is not detected', () => {
        vi.mocked(fakeDetector.get).mockImplementation(key => key !== 'hasDotNet');

        expect(createProvider().getServerStatus('.NET')).toBe('disabled');
    });

    it('returns available when at least one scope in the server is available', () => {
        // Workspace has git (contextKey: hasGit) and editorconfig (no contextKey).
        // Even if git is disabled by context, editorconfig keeps the server available.
        vi.mocked(fakeDetector.get).mockImplementation(key => key !== 'hasGit');

        expect(createProvider().getServerStatus('Workspace')).toBe('available');
    });

    it('returns disabled when all tool settings for the server are set to false', () => {
        __setConfigStore({
            'autocontext.mcpTools.check_csharp_async_patterns': false,
            'autocontext.mcpTools.check_csharp_coding_style': false,
            'autocontext.mcpTools.check_csharp_member_ordering': false,
            'autocontext.mcpTools.check_csharp_naming_conventions': false,
            'autocontext.mcpTools.check_csharp_nullable_context': false,
            'autocontext.mcpTools.check_csharp_project_structure': false,
            'autocontext.mcpTools.check_csharp_test_style': false,
            'autocontext.mcpTools.check_nuget_hygiene': false,
        });

        expect(createProvider().getServerStatus('.NET')).toBe('disabled');
    });

    it('returns unavailable for an unknown server label', () => {
        expect(createProvider().getServerStatus('Unknown')).toBe('unavailable');
    });

    it('returns unavailable for web when index.js exists but node_modules is missing', () => {
        existsSyncMock.mockImplementation(p => !String(p).includes('node_modules'));

        expect(createProvider().getServerStatus('Web')).toBe('unavailable');
    });
});
