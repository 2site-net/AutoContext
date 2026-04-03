export interface McpServerEntry {
    label: string;
    category: string;
    process: 'dotnet' | 'web' | 'workspace';
    contextKey?: string;
}
