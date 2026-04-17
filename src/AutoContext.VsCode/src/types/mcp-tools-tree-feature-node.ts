import type { McpToolsCatalogEntry } from '../mcp-tools-catalog-entry.js';
import type { TreeViewNodeState } from '../tree-view-node-state.js';

export interface McpToolsTreeFeatureNode {
    readonly kind: 'mcpToolFeatureNode';
    readonly entry: McpToolsCatalogEntry;
    readonly state: TreeViewNodeState;
}
