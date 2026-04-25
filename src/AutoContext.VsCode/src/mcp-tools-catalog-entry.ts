import type { CatalogEntry } from './types/catalog-entry.js';
import type { McpCategory } from './types/mcp-category.js';
import type { McpToolsEntry } from './types/mcp-tools-entry.js';

export class McpToolsCatalogEntry implements CatalogEntry {
    readonly contextKey: string;
    readonly toolName: string;
    readonly taskName?: string;
    readonly label: string;
    readonly leafCategory: McpCategory;
    readonly workerCategory: McpCategory;
    readonly workspaceFlags?: readonly string[];
    readonly description?: string;

    constructor(data: McpToolsEntry, description?: string) {
        this.contextKey = `autocontext.mcpTools.${data.key}`;
        this.toolName = data.toolName ?? data.key;
        this.taskName = data.toolName ? data.key : undefined;
        this.label = data.label;
        this.leafCategory = data.leafCategory;
        this.workerCategory = data.workerCategory;
        this.workspaceFlags = data.workspaceFlags;
        this.description = description;
    }
}
