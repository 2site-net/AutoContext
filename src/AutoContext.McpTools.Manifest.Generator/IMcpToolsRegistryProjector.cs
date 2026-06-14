namespace AutoContext.McpTools.Manifest.Generator;

/// <summary>
/// Projects the hand-authored <c>mcp-tools-registry.json</c> into the wire-shape
/// <c>mcp-tools.json</c> catalog.
/// </summary>
internal interface IMcpToolsRegistryProjector
{
    /// <summary>
    /// Reads the registry at <paramref name="registryPath"/> and flattens its
    /// worker groups into a single tool catalog, preserving registry declaration
    /// order.
    /// </summary>
    /// <param name="registryPath">The path to <c>mcp-tools-registry.json</c>.</param>
    /// <returns>The projected catalog.</returns>
    /// <exception cref="ArgumentException"><paramref name="registryPath"/> is
    /// <see langword="null"/> or empty.</exception>
    /// <exception cref="InvalidOperationException">The registry is missing,
    /// unparsable, empty, declares a tool or task without a name, declares a tool
    /// without a description, or declares the same tool name twice.</exception>
    JsonMcpToolsCatalog Project(string registryPath);
}
