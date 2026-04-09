export interface McpToolsEntry {
    key: string;
    toolName?: string;
    label: string;
    category: string;
    group: string;
    serverCategory: string;
    contextKeys?: readonly string[];
}
