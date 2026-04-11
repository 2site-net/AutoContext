namespace AutoContext.Mcp.Shared.EditorConfig.Protocol;

/// <summary>
/// Response containing the resolved mode and data for each requested MCP tool.
/// </summary>
/// <param name="McpTools">Per-tool results with mode and optional data.</param>
internal sealed record McpToolsResponse(McpToolEditorConfigResult[] McpTools);
