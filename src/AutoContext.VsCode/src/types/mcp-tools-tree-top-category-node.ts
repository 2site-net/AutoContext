import type { McpToolsTreeSubCategoryNode } from './mcp-tools-tree-sub-category-node.js';

export interface McpToolsTreeTopCategoryNode {
    readonly kind: 'mcpTopCategoryNode';
    readonly name: string;
    readonly workerId: string | undefined;
    readonly children: readonly McpToolsTreeSubCategoryNode[];
    readonly totalEntries: number;
}
