namespace SharpPilot.Mcp.DotNet.Protocol;

/// <summary>
/// Response containing the resolved mode and data for each requested MCP tool.
/// </summary>
/// <param name="McpTools">Per-tool results with mode and optional data.</param>
internal sealed record McpToolsResponse(McpToolEditorConfigResult[] McpTools);
