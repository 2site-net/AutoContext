import type { McpToolEntry } from './mcp-tool-entry.js';

const categories: Record<string, readonly string[]> = {
    dotnet: ['NuGet', 'C#'],
    git: ['Git'],
    editorconfig: ['EditorConfig'],
    typescript: ['TypeScript'],
};

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

    getSettingIdByCategory(category: string): readonly string[] {
        const cats = categories[category];
        return cats ? this.entries.filter(t => cats.includes(t.category)).map(t => t.settingId) : [];
    }
}
