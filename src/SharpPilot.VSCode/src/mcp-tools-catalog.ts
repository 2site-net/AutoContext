import type { McpToolEntry } from './mcp-tool-entry.js';

export class McpToolsCatalog {
    private readonly entries: readonly McpToolEntry[];

    constructor(
        data: readonly McpToolEntry[],
        private readonly serverCategories: Record<string, readonly string[]>,
    ) {
        this.entries = data;
    }

    get all(): readonly McpToolEntry[] {
        return this.entries;
    }

    get count(): number {
        return this.entries.length;
    }

    getSettingIdByCategory(category: string): readonly string[] {
        const cats = this.serverCategories[category];
        return cats ? this.entries.filter(t => cats.includes(t.category)).map(t => t.settingId) : [];
    }
}
