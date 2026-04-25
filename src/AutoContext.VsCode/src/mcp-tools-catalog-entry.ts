import type { CatalogEntry } from './types/catalog-entry.js';
import type { McpToolsEntry } from './types/mcp-tools-entry.js';
import type { McpToolsMetadataEntry } from './types/mcp-tools-metadata-entry.js';

export class McpToolsCatalogEntry implements CatalogEntry {
    readonly contextKey: string;
    readonly toolName: string;
    readonly taskName?: string;
    readonly label: string;
    readonly category: string;
    readonly serverLabel: string;
    readonly workspaceFlags?: readonly string[];
    readonly description?: string;

    constructor(data: McpToolsEntry, metadata?: McpToolsMetadataEntry) {
        this.contextKey = `autocontext.mcpTools.${data.key}`;
        this.toolName = data.toolName ?? data.key;
        this.taskName = data.toolName ? data.key : undefined;
        this.label = data.label;
        this.category = data.category;
        this.serverLabel = data.serverLabel;
        this.workspaceFlags = data.workspaceFlags;
        this.description = metadata?.description;
    }
}
