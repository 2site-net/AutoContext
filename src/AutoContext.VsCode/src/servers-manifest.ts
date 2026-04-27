import type { ServerEntry } from './server-entry.js';

/**
 * Fully-resolved, in-memory representation of `resources/servers.json`.
 * Built by `ServersManifestLoader.load()`. The shape mirrors the JSON
 * exactly: a flat list of {@link ServerEntry} values addressable by id.
 *
 * Higher-level views (e.g. "which servers are workers?") live at the
 * boot site — they depend on cross-referencing other manifests
 * (`mcp-tools.json`) and don't belong here.
 */
export class ServersManifest {
    #byId?: ReadonlyMap<string, ServerEntry>;

    constructor(readonly servers: readonly ServerEntry[]) {}

    byId(id: string): ServerEntry | undefined {
        return (this.#byId ??= new Map(this.servers.map(s => [s.id, s]))).get(id);
    }

    /**
     * The single `mcp-server` entry. Throws if `servers.json` does not
     * contain it — Mcp.Server is a hard requirement of the extension.
     */
    get mcpServer(): ServerEntry {
        const entry = this.byId('mcp-server');
        if (!entry) {
            throw new Error("servers.json is missing an entry with id 'mcp-server'.");
        }
        return entry;
    }
}
