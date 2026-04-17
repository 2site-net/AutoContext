export interface McpToolsEntry {
    key: string;
    toolName?: string;
    label: string;
    category: string;
    serverLabel: string;
    scope: string;
    contextKeys?: readonly string[];
}
