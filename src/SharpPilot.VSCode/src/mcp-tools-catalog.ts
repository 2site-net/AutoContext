import { McpToolsCatalogEntry } from './mcp-tools-catalog-entry.js';
import type { McpToolsEntry } from './types/mcp-tools-entry.js';

export class McpToolsCatalog {
    private readonly entries: readonly McpToolsCatalogEntry[];

    constructor(data: readonly McpToolsEntry[]) {
        this.entries = data.map(d => new McpToolsCatalogEntry(d));
    }

    get all(): readonly McpToolsCatalogEntry[] {
        return this.entries;
    }

    get count(): number {
        return this.entries.length;
    }

    getSettingIdByCategory(serverCategory: string): readonly string[] {
        return this.entries.filter(t => t.serverCategory === serverCategory).map(t => t.settingId);
    }
}
