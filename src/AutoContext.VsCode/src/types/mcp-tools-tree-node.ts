import type { McpTaskTreeNode } from './mcp-task-tree-node.js';

export interface McpToolsTreeNode {
    readonly kind: 'mcpToolNode';
    readonly toolName: string;
    readonly category: string;
    readonly tasks: readonly McpTaskTreeNode[];
    /**
     * True when the tool has exactly one task whose name equals the tool's
     * own name. Such tools render as a single checkable leaf row instead of
     * an expandable parent with one child.
     */
    readonly isLeaf: boolean;
}
