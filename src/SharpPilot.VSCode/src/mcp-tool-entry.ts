import type { CatalogEntry } from './catalog-entry.js';

export interface McpToolEntry extends CatalogEntry {
    toolName: string;
    group: string;
}
