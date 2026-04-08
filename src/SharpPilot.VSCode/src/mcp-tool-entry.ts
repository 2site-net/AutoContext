import type { CatalogEntry } from './catalog-entry.js';

export interface McpToolEntry extends CatalogEntry {
    featureName?: string;
    toolName: string;
    group: string;
    serverCategory: string;
}
