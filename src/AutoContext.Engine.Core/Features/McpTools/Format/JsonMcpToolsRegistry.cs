namespace AutoContext.Engine.Core.Features.McpTools.Format;

/// <summary>
/// Disk read-model for the hand-authored <c>mcp-tools-registry.json</c>
/// side-car the engine ships beside its binary: a schema version plus a
/// flat list of MCP tool definitions, in document order. Mirrors the root
/// shape in <c>mcp-tools-registry.schema.json</c>. The instance
/// <c>$schema</c> property is editor metadata only and is intentionally
/// not modelled.
/// </summary>
/// <param name="SchemaVersion">The registry format version.</param>
/// <param name="Tools">The MCP tool definitions, in document order.</param>
internal sealed record JsonMcpToolsRegistry(
    string? SchemaVersion = null,
    IReadOnlyList<JsonMcpToolsRegistryTool>? Tools = null);
