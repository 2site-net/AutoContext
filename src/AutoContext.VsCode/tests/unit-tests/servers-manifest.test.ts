import { describe, it, expect } from 'vitest';
import { ServersManifest } from '../../src/servers-manifest';
import type { ServerEntry } from '../../src/types/server-entry';

const allServers: ServerEntry[] = [
    { name: 'AutoContext.Mcp.DotNet', type: 'dotnet' },
    { name: 'AutoContext.WorkspaceServer', type: 'dotnet' },
    { name: 'AutoContext.Mcp.Web', type: 'node' },
    { id: 'mcp-server', name: 'AutoContext.Mcp.Server', type: 'dotnet' },
    { id: 'workspace', name: 'AutoContext.Worker.Workspace', type: 'dotnet' },
    { id: 'dotnet', name: 'AutoContext.Worker.DotNet', type: 'dotnet' },
    { id: 'web', name: 'AutoContext.Worker.Web', type: 'node' },
];

const workerIds = new Set(['workspace', 'dotnet', 'web']);

describe('ServersManifest.workers()', () => {
    it('returns only entries whose id appears in the tools manifest worker set', () => {
        const manifest = new ServersManifest(allServers, workerIds);

        expect(manifest.workers().map(e => e.id)).toEqual(['workspace', 'dotnet', 'web']);
    });

    it('excludes entries that have no id', () => {
        const manifest = new ServersManifest(allServers, workerIds);

        expect(manifest.workers().every(e => e.id !== undefined)).toBe(true);
    });

    it('excludes entries whose id is not in the worker set (e.g. mcp-server)', () => {
        const manifest = new ServersManifest(allServers, workerIds);

        expect(manifest.workers().find(e => e.id === 'mcp-server')).toBeUndefined();
    });

    it('returns an empty array when no entries match the worker set', () => {
        const manifest = new ServersManifest(allServers, new Set());

        expect(manifest.workers()).toHaveLength(0);
    });
});
