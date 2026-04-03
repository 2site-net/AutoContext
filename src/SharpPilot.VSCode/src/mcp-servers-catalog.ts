import type { McpServerEntry } from './mcp-server-entry.js';

export class McpServersCatalog {
    private readonly entries: readonly McpServerEntry[];

    constructor(data: readonly McpServerEntry[]) {
        this.entries = data;
    }

    get all(): readonly McpServerEntry[] {
        return this.entries;
    }
}
