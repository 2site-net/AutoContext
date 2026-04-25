import { describe, it, expect, vi, beforeEach } from 'vitest';
import { join } from 'node:path';
import { McpStdioServerDefinition } from './_fakes/fake-vscode';
import { McpServerProvider } from '../../src/mcp-server-provider';
import { McpToolsManifestLoader } from '../../src/mcp-tools-manifest-loader';
import type { ServersManifest } from '../../src/types/servers-manifest';
import type { AutoContextConfig } from '../../src/types/autocontext-config';
import type { WorkerManager } from '../../src/worker-manager';
import { createFakeConfigManager, createFakeHealthMonitor, createFakeOutputChannel } from './_fakes';

const { existsSyncMock } = vi.hoisted(() => ({ existsSyncMock: vi.fn<(path: string) => boolean>(() => true) }));
vi.mock('node:fs', async () => {
    const actual = await vi.importActual<typeof import('node:fs')>('node:fs');
    return { ...actual, existsSync: existsSyncMock };
});

type StdioDef = InstanceType<typeof McpStdioServerDefinition>;

const extensionPath = '/ext';
const version = '1.0.0';

const fakeHealthMonitor = createFakeHealthMonitor();
const outputChannel = createFakeOutputChannel();

const onDidChange = vi.fn() as unknown as import('vscode').Event<void>;
const mcpToolsManifest = new McpToolsManifestLoader(join(__dirname, '..', '..')).load();
const serversManifest: ServersManifest = {
    workers: [],
    mcpServer: { id: 'mcp-server', name: 'AutoContext.Mcp.Server', type: 'dotnet' },
};
const fakeWorkerManager = { getEndpointSuffix: vi.fn(() => 'abc123def456') } as unknown as WorkerManager;

let currentConfig: AutoContextConfig = {};
const fakeConfigManager = createFakeConfigManager();

function createProvider(): McpServerProvider {
    return new McpServerProvider(
        extensionPath,
        version,
        onDidChange,
        mcpToolsManifest,
        fakeHealthMonitor,
        fakeWorkerManager,
        serversManifest,
        fakeConfigManager,
        outputChannel,
    );
}

beforeEach(() => {
    vi.clearAllMocks();
    currentConfig = {};
    vi.mocked(fakeConfigManager.readSync).mockImplementation(() => currentConfig);
    vi.mocked(fakeConfigManager.onDidChange).mockReturnValue({ dispose: vi.fn() });
    existsSyncMock.mockReturnValue(true);
    vi.mocked(fakeHealthMonitor.getPipeName).mockReturnValue('autocontext-health-abc123');
    vi.mocked(fakeWorkerManager.getEndpointSuffix).mockReturnValue('abc123def456');
});

function buildAllDisabledConfig(): AutoContextConfig {
    const mcpToolsConfig: NonNullable<AutoContextConfig['mcpTools']> = {};
    for (const tool of mcpToolsManifest.tools) {
        if (tool.tasks.length === 0) {
            mcpToolsConfig[tool.name] = false;
        } else {
            mcpToolsConfig[tool.name] = { disabledTasks: tool.tasks.map(t => t.name) };
        }
    }
    return { mcpTools: mcpToolsConfig };
}

describe('McpServerProvider.provideMcpServerDefinitions', () => {
    it('returns a single definition when binary exists and any tool is enabled', async () => {
        const defs = await createProvider().provideMcpServerDefinitions();

        expect(defs).toHaveLength(1);
        expect(defs[0]).toBeInstanceOf(McpStdioServerDefinition);
    });

    it('returns an empty list when the Mcp.Server binary does not exist', async () => {
        existsSyncMock.mockReturnValue(false);

        const defs = await createProvider().provideMcpServerDefinitions();

        expect(defs).toHaveLength(0);
    });

    it('returns an empty list when every tool is disabled', async () => {
        currentConfig = buildAllDisabledConfig();

        const defs = await createProvider().provideMcpServerDefinitions();

        expect(defs).toHaveLength(0);
    });

    it('resolves to AutoContext.Mcp.Server binary', async () => {
        const [def] = (await createProvider().provideMcpServerDefinitions()) as StdioDef[];

        expect(def.command).toContain('AutoContext.Mcp.Server');
    });

    it('passes --endpoint-suffix with the WorkerManager suffix', async () => {
        const [def] = (await createProvider().provideMcpServerDefinitions()) as StdioDef[];

        expect(def.args).toEqual(expect.arrayContaining(['--endpoint-suffix', 'abc123def456']));
    });

    it('passes --health-monitor with the health-monitor pipe name', async () => {
        const [def] = (await createProvider().provideMcpServerDefinitions()) as StdioDef[];

        expect(def.args).toEqual(expect.arrayContaining(['--health-monitor', 'autocontext-health-abc123']));
    });

    it('does not pass --scope, --workspace-folder, or --workspace-server', async () => {
        const [def] = (await createProvider().provideMcpServerDefinitions()) as StdioDef[];

        expect(def.args).not.toContain('--scope');
        expect(def.args).not.toContain('--workspace-folder');
        expect(def.args).not.toContain('--workspace-server');
    });

    it('carries the extension version', async () => {
        const [def] = (await createProvider().provideMcpServerDefinitions()) as StdioDef[];

        expect(def.version).toBe(version);
    });
});

describe('McpServerProvider.getServerStatus', () => {
    it('returns available when the binary exists and at least one tool is enabled', () => {
        expect(createProvider().getServerStatus('.NET')).toBe('available');
    });

    it('returns unavailable when the binary does not exist', () => {
        existsSyncMock.mockReturnValue(false);

        expect(createProvider().getServerStatus('.NET')).toBe('unavailable');
    });

    it('returns disabled when every tool is turned off in settings', () => {
        currentConfig = buildAllDisabledConfig();

        expect(createProvider().getServerStatus('.NET')).toBe('disabled');
    });

    it('returns the same status regardless of the legacy label passed', () => {
        const provider = createProvider();

        expect(provider.getServerStatus('.NET')).toBe('available');
        expect(provider.getServerStatus('Web')).toBe('available');
        expect(provider.getServerStatus('Workspace')).toBe('available');
        expect(provider.getServerStatus('Anything')).toBe('available');
    });
});

describe('McpServerProvider.getDefinitionIds', () => {
    it('returns the single Mcp.Server definition id for any label', () => {
        const provider = createProvider();
        const expected = ['2site-net.autocontext/AutoContext'];

        expect(provider.getDefinitionIds('.NET')).toEqual(expected);
        expect(provider.getDefinitionIds('Web')).toEqual(expected);
        expect(provider.getDefinitionIds('Workspace')).toEqual(expected);
        expect(provider.getDefinitionIds('Unknown')).toEqual(expected);
    });
});

describe('McpServerProvider config updates', () => {
    it('logs to the output channel when configManager.read rejects', async () => {
        let onDidChangeCallback!: () => void;
        const failingConfigManager = {
            readSync: vi.fn(() => ({})),
            read: vi.fn().mockRejectedValue(new Error('read boom')),
            onDidChange: vi.fn((cb: () => void) => { onDidChangeCallback = cb; return { dispose: vi.fn() }; }),
        } as unknown as import('../../src/autocontext-config').AutoContextConfigManager;

        const oc = createFakeOutputChannel();
        const provider = new McpServerProvider(
            extensionPath,
            version,
            onDidChange,
            mcpToolsManifest,
            fakeHealthMonitor,
            fakeWorkerManager,
            serversManifest,
            failingConfigManager,
            oc,
        );

        onDidChangeCallback();
        await vi.waitFor(() => {
            expect(oc.appendLine).toHaveBeenCalledWith(
                expect.stringContaining('[McpServerProvider] Failed to update config: read boom'),
            );
        });

        provider.dispose();
    });
});
