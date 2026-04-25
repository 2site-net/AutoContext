import type { ServerEntry } from './server-entry.js';

/**
 * Derived view of `servers.json` cross-referenced with
 * `resources/mcp-tools.json`. `workers` contains the servers whose `id`
 * is a `workerId` in the tools manifest; `mcpServer` is the single
 * `AutoContext.Mcp.Server` entry VS Code spawns.
 */
export interface ServersManifest {
    readonly workers: readonly (ServerEntry & { id: string })[];
    readonly mcpServer: ServerEntry & { id: string };
}
