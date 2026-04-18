import { McpToolsCatalogEntry } from './mcp-tools-catalog-entry.js';
import type { McpToolsMetadataEntry } from './mcp-tools-catalog-entry.js';
import type { McpToolsEntry } from './types/mcp-tools-entry.js';

export class McpToolsCatalog {
    private readonly entries: readonly McpToolsCatalogEntry[];
    private readonly metadata?: ReadonlyMap<string, McpToolsMetadataEntry>;

    constructor(data: readonly McpToolsEntry[], metadata?: ReadonlyMap<string, McpToolsMetadataEntry>) {
        this.entries = data.map(d => new McpToolsCatalogEntry(d, metadata?.get(d.key)));
        this.metadata = metadata;
    }

    get all(): readonly McpToolsCatalogEntry[] {
        return this.entries;
    }

    get count(): number {
        return this.entries.length;
    }

    getMetadata(toolName: string): McpToolsMetadataEntry | undefined {
        return this.metadata?.get(toolName);
    }

    getContextKeysByScope(scope: string): readonly string[] {
        return this.entries.filter(t => t.scope === scope).map(t => t.contextKey);
    }

    getEntriesByScope(scope: string): readonly McpToolsCatalogEntry[] {
        return this.entries.filter(t => t.scope === scope);
    }
}
