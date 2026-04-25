import { McpToolsCatalogEntry } from './mcp-tools-catalog-entry.js';
import type { McpToolsCatalogOptions } from './types/mcp-tools-catalog-options.js';
import type { McpToolsEntry } from './types/mcp-tools-entry.js';
import type { McpToolsMetadataEntry } from './types/mcp-tools-metadata-entry.js';

export class McpToolsCatalog {
    private readonly entries: readonly McpToolsCatalogEntry[];
    private readonly metadata?: ReadonlyMap<string, McpToolsMetadataEntry>;
    readonly serverLabelOrder: readonly string[];
    readonly categoryOrder: readonly string[];
    readonly serverLabelToWorkerIdMap: ReadonlyMap<string, string>;

    constructor(data: readonly McpToolsEntry[], options: McpToolsCatalogOptions = {}) {
        this.entries = data.map(d => new McpToolsCatalogEntry(d, options.metadata?.get(d.key)));
        this.metadata = options.metadata;
        this.serverLabelOrder = options.serverLabelOrder ?? this.derivedServerLabelOrder();
        this.categoryOrder = options.categoryOrder ?? this.derivedCategoryOrder();
        this.serverLabelToWorkerIdMap = options.serverLabelToWorkerIdMap ?? new Map();
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

    private derivedServerLabelOrder(): readonly string[] {
        return [...new Set(this.entries.map(e => e.serverLabel))];
    }

    private derivedCategoryOrder(): readonly string[] {
        return [...new Set(this.entries.map(e => e.category))];
    }
}
