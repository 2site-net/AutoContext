import type { McpTaskTreeNode } from './mcp-task-tree-node.js';
import type { TreeViewNodeState } from '../tree-view-node-state.js';

export interface McpToolsTreeNode {
    readonly kind: 'mcpToolNode';
    readonly toolName: string;
    readonly category: string;
    readonly tasks: readonly McpTaskTreeNode[];
    readonly leafState?: TreeViewNodeState;
}
