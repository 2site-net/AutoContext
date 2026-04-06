import type { CatalogEntry } from './catalog-entry.js';

export interface McpToolEntry extends CatalogEntry {
    toolName: string;
    aggregationTool: string;
    group: string;
}
