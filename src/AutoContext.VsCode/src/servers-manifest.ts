import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import type { ServerEntry } from './types/server-entry.js';

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
 * Loaded representation of `servers.json`, cross-referenced with
 * `mcp-tools-manifest.json` to identify which entries are workers.
 */
export class ServersManifest {
    /**
     * Reads `servers.json` and `mcp-tools-manifest.json` from the extension
     * directory and constructs the manifest.
     */
    static load(extensionPath: string): ServersManifest {
        const rawServers: RawServersFile = JSON.parse(
            readFileSync(join(extensionPath, 'servers.json'), 'utf-8'),
        );
        const rawTools: RawToolsManifest = JSON.parse(
            readFileSync(join(extensionPath, 'mcp-tools-manifest.json'), 'utf-8'),
        );

        const workerIds = new Set(
            rawTools.categories
                .filter(c => c.workerId !== undefined)
                .map(c => c.workerId!),
        );

        return new ServersManifest(rawServers.servers, workerIds);
    }

    constructor(
        private readonly allServers: readonly ServerEntry[],
        private readonly workerIds: ReadonlySet<string>,
    ) {}

    /**
     * Returns the entries that correspond to a worker process — those whose
     * `id` appears as a `workerId` in `mcp-tools-manifest.json`. The narrowed
     * return type guarantees each entry has a defined `id`.
     */
    workers(): readonly (ServerEntry & { id: string })[] {
        return this.allServers.filter(
            (s): s is ServerEntry & { id: string } =>
                s.id !== undefined && this.workerIds.has(s.id),
        );
    }
}
