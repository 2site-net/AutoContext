export interface McpToolsRequest {
    readonly tools: readonly string[];
    readonly filePath?: string;
    readonly 'editorconfig-keys'?: readonly string[];
}
