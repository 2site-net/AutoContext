namespace AutoContext.Engine.Core.Features.McpTools.Format;

/// <summary>
/// Disk read-model for the hand-authored <c>mcp-tools-catalog.json</c>
/// curatorial side-car the engine reads at startup: a schema version, the
/// hierarchical <see cref="Categories"/> taxonomy, and one
/// <see cref="JsonMcpToolsCatalogTool"/> per cataloged tool. Mirrors the
/// root shape in <c>mcp-tools-catalog.schema.json</c>. The instance
/// <c>$schema</c> property is editor metadata only and is intentionally
/// not modelled. The loader merges this layer onto the
/// <see cref="JsonMcpToolsRegistry"/> execution facts by tool name.
/// </summary>
/// <param name="SchemaVersion">The catalog format version.</param>
/// <param name="Categories">The category taxonomy definitions, in
/// document order.</param>
/// <param name="Tools">The per-tool curatorial entries, in document
/// order.</param>
internal sealed record JsonMcpToolsCatalog(
    string? SchemaVersion = null,
    IReadOnlyList<JsonMcpToolsCatalogCategory>? Categories = null,
    IReadOnlyList<JsonMcpToolsCatalogTool>? Tools = null);
