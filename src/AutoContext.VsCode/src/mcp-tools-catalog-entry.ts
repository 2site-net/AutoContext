import type { CatalogEntry } from './types/catalog-entry.js';
import type { McpToolsEntry } from './types/mcp-tools-entry.js';

export interface McpToolsMetadataEntry {
    readonly description?: string;
    readonly version?: string;
}

export class McpToolsCatalogEntry implements CatalogEntry {
    readonly settingId: string;
    readonly toolName: string;
    readonly featureName?: string;
    readonly label: string;
    readonly category: string;
    readonly serverLabel: string;
    readonly scope: string;
    readonly contextKeys?: readonly string[];
    readonly description?: string;
    readonly version?: string;

    constructor(data: McpToolsEntry, metadata?: McpToolsMetadataEntry) {
        this.settingId = `autocontext.mcpTools.${data.key}`;
        this.toolName = data.toolName ?? data.key;
        this.featureName = data.toolName ? data.key : undefined;
        this.label = data.label;
        this.category = data.category;
        this.serverLabel = data.serverLabel;
        this.scope = data.scope;
        this.contextKeys = data.contextKeys;
        this.description = metadata?.description;
        this.version = metadata?.version;
    }
}
