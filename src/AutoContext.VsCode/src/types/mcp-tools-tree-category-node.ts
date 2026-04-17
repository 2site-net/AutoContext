import type { McpToolsTreeNode } from './mcp-tools-tree-node.js';

export interface McpToolsTreeCategoryNode {
    readonly kind: 'categoryNode';
    readonly serverLabel: string;
    readonly name: string;
    readonly children: readonly McpToolsTreeNode[];
    readonly totalEntries: number;
}
