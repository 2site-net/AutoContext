import type { McpToolsTreeCategoryNode } from './mcp-tools-tree-category-node.js';

export interface TreeViewServerLabelNode {
    readonly kind: 'serverNode';
    readonly name: string;
    readonly workerId: string | undefined;
    readonly children: readonly McpToolsTreeCategoryNode[];
    readonly totalEntries: number;
}
