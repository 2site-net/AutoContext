namespace AutoContext.Mcp.Shared.McpTools;

/// <summary>
/// The resolved mode and optional EditorConfig data for a single MCP tool.
/// </summary>
/// <param name="Name">Tool name echoed from the request.</param>
/// <param name="Mode">The orchestration decision for this tool.</param>
/// <param name="Data">Resolved EditorConfig properties when <see cref="Mode"/> is not <see cref="McpToolMode.Skip"/>.</param>
internal sealed record McpToolResult(string Name, McpToolMode Mode, Dictionary<string, string>? Data = null);
