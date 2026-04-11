import type { CatalogEntry } from './types/catalog-entry.js';
import type { McpToolsEntry } from './types/mcp-tools-entry.js';

export class McpToolsCatalogEntry implements CatalogEntry {
    readonly settingId: string;
    readonly toolName: string;
    readonly featureName?: string;
    readonly label: string;
    readonly category: string;
    readonly group: string;
    readonly serverCategory: string;
    readonly contextKeys?: readonly string[];

    constructor(data: McpToolsEntry) {
        this.settingId = `autocontext.mcpTools.${data.key}`;
        this.toolName = data.toolName ?? data.key;
        this.featureName = data.toolName ? data.key : undefined;
        this.label = data.label;
        this.category = data.category;
        this.group = data.group;
        this.serverCategory = data.serverCategory;
        this.contextKeys = data.contextKeys;
    }
}
