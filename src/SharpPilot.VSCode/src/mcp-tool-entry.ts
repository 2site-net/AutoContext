export interface McpToolEntry {
    key: string;
    toolName?: string;
    label: string;
    category: string;
    group: string;
    serverCategory: string;
    contextKeys?: readonly string[];
}
