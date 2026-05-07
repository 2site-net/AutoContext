/**
 * Synthetic top-of-tree node representing the AutoContext MCP server
 * (the central process that hosts all worker tools). Rendered as the
 * first row in the MCP Tools view above the worker top categories.
 */
export interface McpServerTreeNode {
    readonly kind: 'mcpServerNode';
    /**
     * Server name used by the `autocontext.show-mcp-server-output`
     * command handler to look up the VS Code MCP definition id.
     */
    readonly name: string;
}
