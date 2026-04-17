export interface McpServerEntry {
    label: string;
    scope: string;
    server: 'dotnet' | 'web' | 'workspace';
    contextKey?: string;
}
