export interface McpToolsResponse {
    readonly tools: Record<string, boolean>;
    readonly editorconfig?: Record<string, string>;
}
