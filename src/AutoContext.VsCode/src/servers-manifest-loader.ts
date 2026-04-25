import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import type { ServerEntry } from './types/server-entry.js';
import type { ServersManifest } from './types/servers-manifest.js';

interface RawServersFile {
    servers: ServerEntry[];
}

interface RawToolsCategory {
    workerId?: string;
}

interface RawToolsManifest {
    categories: RawToolsCategory[];
}

/**
 * Loads `servers.json` and cross-references it with
 * `mcp-tools-manifest.json` to produce a {@link ServersManifest}.
 */
export class ServersManifestLoader {
    constructor(private readonly extensionPath: string) {}

    load(): ServersManifest {
        const rawServers: RawServersFile = JSON.parse(
            readFileSync(join(this.extensionPath, 'servers.json'), 'utf-8'),
        );
        const rawTools: RawToolsManifest = JSON.parse(
            readFileSync(join(this.extensionPath, 'mcp-tools-manifest.json'), 'utf-8'),
        );

        const workerIds = new Set(
            rawTools.categories
                .filter(c => c.workerId !== undefined)
                .map(c => c.workerId!),
        );

        const workers = rawServers.servers.filter(
            (s): s is ServerEntry & { id: string } =>
                s.id !== undefined && workerIds.has(s.id),
        );

        const mcpServerEntry = rawServers.servers.find(s => s.id === 'mcp-server');
        if (!mcpServerEntry) {
            throw new Error("servers.json is missing an entry with id 'mcp-server'.");
        }

        return {
            workers,
            mcpServer: mcpServerEntry as ServerEntry & { id: string },
        };
    }
}
