import type { McpToolEntry } from './mcp-tool-entry.js';

export class McpToolsCatalog {
    private readonly entries: readonly McpToolEntry[];

    constructor(data: readonly McpToolEntry[]) {
        this.entries = data;
    }

    get all(): readonly McpToolEntry[] {
        return this.entries;
    }

    get count(): number {
        return this.entries.length;
    }

    getSettingIdByCategory(serverCategory: string): readonly string[] {
        return this.entries.filter(t => t.serverCategory === serverCategory).map(t => t.settingId);
    }
}
