import type { McpToolsTreeCategoryNode } from './mcp-tools-tree-category-node.js';

export interface TreeViewGroupNode {
    readonly kind: 'group';
    readonly name: string;
    readonly children: readonly McpToolsTreeCategoryNode[];
    readonly totalEntries: number;
}
