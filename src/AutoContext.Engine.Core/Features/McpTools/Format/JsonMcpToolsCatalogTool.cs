namespace AutoContext.Engine.Core.Features.McpTools.Format;

/// <summary>
/// Disk read-model for one entry of the <c>tools</c> array in
/// <c>mcp-tools-catalog.json</c> — the curatorial layer for a single MCP
/// tool, keyed by <see cref="Name"/> onto the registry execution facts.
/// Mirrors the <c>tool</c> shape in <c>mcp-tools-catalog.schema.json</c>:
/// the human-facing <see cref="Description"/> (independent of the
/// registry's model-facing description) and the <see cref="Category"/> the
/// tool belongs to.
/// </summary>
/// <param name="Name">The MCP tool name (snake_case); the join key onto
/// the registry.</param>
/// <param name="Description">The human-facing tool description.</param>
/// <param name="Category">The category name this tool belongs to; must
/// resolve to a declared category.</param>
internal sealed record JsonMcpToolsCatalogTool(
    string? Name = null,
    string? Description = null,
    string? Category = null);
