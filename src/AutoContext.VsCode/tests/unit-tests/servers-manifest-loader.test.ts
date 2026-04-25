import { describe, it, expect, vi, beforeEach } from 'vitest';

const { readFileSyncMock } = vi.hoisted(() => ({ readFileSyncMock: vi.fn<(path: string, encoding: string) => string>() }));
vi.mock('node:fs', async () => {
    const actual = await vi.importActual<typeof import('node:fs')>('node:fs');
    return { ...actual, readFileSync: readFileSyncMock };
});

const { ServersManifestLoader } = await import('../../src/servers-manifest-loader');

const allServers = [
    { id: 'mcp-server', name: 'AutoContext.Mcp.Server', type: 'dotnet' },
    { id: 'workspace', name: 'AutoContext.Worker.Workspace', type: 'dotnet' },
    { id: 'dotnet', name: 'AutoContext.Worker.DotNet', type: 'dotnet' },
    { id: 'web', name: 'AutoContext.Worker.Web', type: 'node' },
];

const toolsCategories = [
    { name: 'Workspace', workerId: 'workspace' },
    { name: 'DotNet', workerId: 'dotnet' },
    { name: 'Web', workerId: 'web' },
    { name: 'Git' },
];

function mockManifests(servers: unknown, categories: unknown): void {
    readFileSyncMock.mockImplementation((path: string) => {
        if (path.endsWith('servers.json')) { return JSON.stringify({ servers }); }
        if (path.endsWith('mcp-tools-manifest.json')) { return JSON.stringify({ categories }); }
        throw new Error(`Unexpected path: ${path}`);
    });
}

beforeEach(() => {
    readFileSyncMock.mockReset();
});

describe('ServersManifestLoader.load()', () => {
    it('returns only entries whose id appears in the tools manifest worker set', () => {
        mockManifests(allServers, toolsCategories);

        const manifest = new ServersManifestLoader('/ext').load();

        expect(manifest.workers.map(e => e.id)).toEqual(['workspace', 'dotnet', 'web']);
    });

    it('excludes the mcp-server entry from workers', () => {
        mockManifests(allServers, toolsCategories);

        const manifest = new ServersManifestLoader('/ext').load();

        expect(manifest.workers.find(e => e.id === 'mcp-server')).toBeUndefined();
    });

    it('returns an empty workers array when no tool category declares a workerId', () => {
        mockManifests(allServers, [{ name: 'Git' }]);

        const manifest = new ServersManifestLoader('/ext').load();

        expect(manifest.workers).toHaveLength(0);
    });

    it('exposes the mcp-server entry via mcpServer', () => {
        mockManifests(allServers, toolsCategories);

        const manifest = new ServersManifestLoader('/ext').load();

        expect(manifest.mcpServer.id).toBe('mcp-server');
        expect(manifest.mcpServer.name).toBe('AutoContext.Mcp.Server');
    });

    it('throws when servers.json has no mcp-server entry', () => {
        mockManifests(allServers.filter(s => s.id !== 'mcp-server'), toolsCategories);

        expect(() => new ServersManifestLoader('/ext').load()).toThrow(/mcp-server/);
    });
});
