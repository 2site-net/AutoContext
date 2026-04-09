import type { McpToolsCatalogEntry } from './mcp-tools-catalog-entry.js';
import type { McpToolState } from './ui-constants.js';

export interface McpToolsTreeFeatureNode {
    readonly kind: 'mcpToolFeature';
    readonly entry: McpToolsCatalogEntry;
    readonly state: McpToolState;
}
