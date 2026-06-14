namespace AutoContext.McpTools.Manifest.Generator;

/// <summary>
/// One MCP tool in <c>mcp-tools-registry.json</c>. The projection carries the
/// <see cref="Name"/>, <see cref="Description"/>, and task names forward; the
/// tool's input <c>parameters</c> are left unmapped because the
/// <c>McpTools.List</c> wire shape deliberately omits input schemas.
/// </summary>
internal sealed class JsonRegistryTool
{
    /// <summary>Gets the tool description advertised to MCP clients.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the MCP tool name.</summary>
    public string? Name { get; init; }

    /// <summary>Gets the tasks this tool dispatches when invoked.</summary>
    public IReadOnlyList<JsonRegistryTask>? Tasks { get; init; }
}
