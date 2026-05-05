import type { McpToolsTreeNode } from './mcp-tools-tree-node.js';

export interface McpToolsTreeSubCategoryNode {
    readonly kind: 'mcpSubCategoryNode';
    readonly name: string;
    readonly children: readonly McpToolsTreeNode[];
    readonly totalEntries: number;
}
