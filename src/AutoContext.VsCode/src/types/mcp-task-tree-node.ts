import type { McpTaskEntry } from '../mcp-task-entry.js';
import type { TreeViewNodeState } from '../tree-view-node-state.js';

export interface McpTaskTreeNode {
    readonly kind: 'mcpTaskNode';
    readonly task: McpTaskEntry;
    readonly state: TreeViewNodeState;
}
