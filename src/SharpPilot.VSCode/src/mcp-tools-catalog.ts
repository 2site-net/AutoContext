import { McpToolCatalogEntry } from './mcp-tool-catalog-entry.js';
import type { McpToolEntry } from './mcp-tool-entry.js';

export class McpToolsCatalog {
    private readonly entries: readonly McpToolCatalogEntry[];

    constructor(data: readonly McpToolEntry[]) {
        this.entries = data.map(d => new McpToolCatalogEntry(d));
    }

    get all(): readonly McpToolCatalogEntry[] {
        return this.entries;
    }

    get count(): number {
        return this.entries.length;
    }

    getSettingIdByCategory(serverCategory: string): readonly string[] {
        return this.entries.filter(t => t.serverCategory === serverCategory).map(t => t.settingId);
    }
}
