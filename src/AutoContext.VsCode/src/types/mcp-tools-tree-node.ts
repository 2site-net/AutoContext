import type { McpToolsTreeFeatureNode } from './mcp-tools-tree-feature-node.js';
import type { TreeViewNodeState } from '../tree-view-node-state.js';

export interface McpToolsTreeNode {
    readonly kind: 'mcpToolNode';
    readonly toolName: string;
    readonly category: string;
    readonly features: readonly McpToolsTreeFeatureNode[];
    readonly leafState?: TreeViewNodeState;
}
