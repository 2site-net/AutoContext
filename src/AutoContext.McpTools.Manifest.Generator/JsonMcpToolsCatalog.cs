namespace AutoContext.McpTools.Manifest.Generator;

/// <summary>
/// The build-generated <c>mcp-tools.json</c> envelope: the flat wire-shape tool
/// catalog the engine reads to answer <c>McpTools.List</c>. The catalog is a pure
/// build output projected from <c>mcp-tools-registry.json</c> — it flattens the
/// registry's worker groups into one tool list and carries only the wire-visible
/// fields. The per-request <c>disabled</c> filter is layered by the engine at
/// runtime, so it is absent here.
/// </summary>
internal sealed class JsonMcpToolsCatalog(string? schemaVersion, IReadOnlyList<JsonMcpToolEntry> tools)
{
    /// <summary>Gets the schema version carried through from the registry.</summary>
    public string? SchemaVersion { get; } = schemaVersion;

    /// <summary>Gets the flattened tool rows, in registry declaration order.</summary>
    public IReadOnlyList<JsonMcpToolEntry> Tools { get; } = tools;
}
