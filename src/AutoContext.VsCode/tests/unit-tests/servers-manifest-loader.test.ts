import { describe, it, expect, vi, beforeEach } from 'vitest';

const { readFileSyncMock } = vi.hoisted(() => ({ readFileSyncMock: vi.fn<typeof import('node:fs').readFileSync>() }));
vi.mock('node:fs', async () => {
    const actual = await vi.importActual<typeof import('node:fs')>('node:fs');
    return { ...actual, readFileSync: readFileSyncMock };
});

const { ServersManifestLoader } = await import('../../src/servers-manifest-loader');
const { ServersManifest } = await import('../../src/servers-manifest');
const { ServerEntry } = await import('../../src/server-entry');

const allServers = [
    { id: 'mcp-server', name: 'AutoContext.Mcp.Server', type: 'dotnet' },
    { id: 'workspace', name: 'AutoContext.Worker.Workspace', type: 'dotnet' },
    { id: 'dotnet', name: 'AutoContext.Worker.DotNet', type: 'dotnet' },
    { id: 'web', name: 'AutoContext.Worker.Web', type: 'node' },
];

function mockServers(servers: unknown): void {
    readFileSyncMock.mockImplementation((path) => {
        const p = path.toString();
        if (p.endsWith('servers.json')) { return JSON.stringify({ servers }); }
        throw new Error(`Unexpected path: ${p}`);
    });
}

beforeEach(() => {
    readFileSyncMock.mockReset();
});

describe('ServersManifestLoader.load()', () => {
    it('returns a ServersManifest containing every entry from servers.json', () => {
        mockServers(allServers);

        const manifest = new ServersManifestLoader('/ext').load();

        expect(manifest).toBeInstanceOf(ServersManifest);
        expect(manifest.servers).toHaveLength(4);
        expect(manifest.servers.every(s => s instanceof ServerEntry)).toBe(true);
        expect(manifest.servers.map(s => s.id)).toEqual(['mcp-server', 'workspace', 'dotnet', 'web']);
    });

    it('preserves name and type for each entry', () => {
        mockServers(allServers);

        const manifest = new ServersManifestLoader('/ext').load();
        const web = manifest.byId('web');

        expect(web?.name).toBe('AutoContext.Worker.Web');
        expect(web?.type).toBe('node');
    });

    it('rejects entries with an unsupported type', () => {
        mockServers([{ id: 'bad', name: 'Bad', type: 'rust' }]);

        expect(() => new ServersManifestLoader('/ext').load()).toThrow(/unsupported type 'rust'/);
    });

    it('throws a contextualised error when servers.json contains malformed JSON', () => {
        readFileSyncMock.mockImplementation((path) => {
            const p = path.toString();
            if (p.endsWith('servers.json')) { return '{ not valid json'; }
            throw new Error(`Unexpected path: ${p}`);
        });

        expect(() => new ServersManifestLoader('/ext').load())
            .toThrow(/Failed to parse JSON from .+servers\.json/);
    });
});

describe('ServersManifest', () => {
    it('exposes mcpServer as a getter', () => {
        mockServers(allServers);

        const manifest = new ServersManifestLoader('/ext').load();

        expect(manifest.mcpServer.id).toBe('mcp-server');
        expect(manifest.mcpServer.name).toBe('AutoContext.Mcp.Server');
    });

    it('throws from mcpServer when servers.json lacks an mcp-server entry', () => {
        mockServers(allServers.filter(s => s.id !== 'mcp-server'));

        const manifest = new ServersManifestLoader('/ext').load();

        expect(() => manifest.mcpServer).toThrow(/mcp-server/);
    });

    it('returns undefined from byId for an unknown id', () => {
        mockServers(allServers);

        const manifest = new ServersManifestLoader('/ext').load();

        expect(manifest.byId('nope')).toBeUndefined();
    });
});
