namespace AutoContext.McpTools.Manifest.Generator;

/// <summary>
/// One MCP task in <c>mcp-tools-registry.json</c>. Only the task <see cref="Name"/>
/// crosses into the wire-shape catalog; the task's <c>editorconfig</c> key
/// bindings are dispatch metadata the projection drops.
/// </summary>
internal sealed class JsonRegistryTask
{
    /// <summary>Gets the MCP task name.</summary>
    public string? Name { get; init; }
}
