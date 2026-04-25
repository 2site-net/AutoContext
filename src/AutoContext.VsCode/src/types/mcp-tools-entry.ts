import type { McpCategory } from './mcp-category.js';

export interface McpToolsEntry {
    readonly key: string;
    readonly toolName?: string;
    readonly label: string;
    readonly leafCategory: McpCategory;
    readonly workerCategory: McpCategory;
    readonly workspaceFlags?: readonly string[];
}
