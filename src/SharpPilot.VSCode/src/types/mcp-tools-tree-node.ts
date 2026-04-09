import type { McpToolsTreeFeatureNode } from './mcp-tools-tree-feature-node.js';
import type { McpToolState } from '../ui-constants.js';

export interface McpToolsTreeNode {
    readonly kind: 'mcpTool';
    readonly toolName: string;
    readonly category: string;
    readonly features: readonly McpToolsTreeFeatureNode[];
    readonly leafState?: McpToolState;
}
