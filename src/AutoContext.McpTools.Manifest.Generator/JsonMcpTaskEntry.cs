namespace AutoContext.McpTools.Manifest.Generator;

/// <summary>
/// One task row carried on <see cref="JsonMcpToolEntry.Tasks"/> in the
/// build-generated <c>mcp-tools.json</c> catalog. Only the task name crosses into
/// the wire-shape catalog; the per-request <c>disabled</c> state is layered by the
/// engine at runtime.
/// </summary>
internal sealed class JsonMcpTaskEntry(string name)
{
    /// <summary>Gets the MCP task name (unique within its parent tool).</summary>
    public string Name { get; } = name;
}
