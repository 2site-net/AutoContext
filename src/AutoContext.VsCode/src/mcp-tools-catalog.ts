import { McpToolsCatalogEntry } from './mcp-tools-catalog-entry.js';
import type { McpToolsCatalogOptions } from './types/mcp-tools-catalog-options.js';
import type { McpToolsEntry } from './types/mcp-tools-entry.js';

export class McpToolsCatalog {
    private readonly entries: readonly McpToolsCatalogEntry[];
    private readonly descriptions?: ReadonlyMap<string, string>;
    readonly serverLabelOrder: readonly string[];
    readonly categoryOrder: readonly string[];
    readonly serverLabelToWorkerIdMap: ReadonlyMap<string, string>;

    constructor(data: readonly McpToolsEntry[], options: McpToolsCatalogOptions = {}) {
        this.entries = data.map(d => new McpToolsCatalogEntry(d, options.descriptions?.get(d.key)));
        this.descriptions = options.descriptions;
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

    getDescription(toolOrTaskName: string): string | undefined {
        return this.descriptions?.get(toolOrTaskName);
    }

    private derivedServerLabelOrder(): readonly string[] {
        return [...new Set(this.entries.map(e => e.workerCategory.name))];
    }

    private derivedCategoryOrder(): readonly string[] {
        return [...new Set(this.entries.map(e => e.leafCategory.name))];
    }
}
