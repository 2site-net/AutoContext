import type { McpToolCatalogEntry } from './mcp-tool-catalog-entry.js';
import type { McpToolState } from './ui-constants.js';

export interface McpToolsTreeFeatureNode {
    readonly kind: 'mcpToolFeature';
    readonly entry: McpToolCatalogEntry;
    readonly state: McpToolState;
}
