import type { McpCategoryEntry } from './mcp-category-entry.js';
import type { McpToolEntry } from './mcp-tool-entry.js';

/**
 * Fully-resolved, in-memory representation of `resources/mcp-tools.json`.
 * Built by `McpToolsManifestLoader.load()`.
 */
export class McpToolsManifest {
    #topCategories?: readonly McpCategoryEntry[];
    #subCategories?: readonly McpCategoryEntry[];
    #toolByName?: ReadonlyMap<string, McpToolEntry>;

    constructor(
        readonly tools: readonly McpToolEntry[],
        readonly categories: readonly McpCategoryEntry[],
    ) {}

    get topCategories(): readonly McpCategoryEntry[] {
        return this.#topCategories ??= this.categories.filter(c => c.isTopLevel);
    }

    get subCategories(): readonly McpCategoryEntry[] {
        return this.#subCategories ??= this.categories.filter(c => !c.isTopLevel);
    }

    toolByName(name: string): McpToolEntry | undefined {
        return (this.#toolByName ??=
            new Map(this.tools.map(t => [t.name, t]))).get(name);
    }
}
