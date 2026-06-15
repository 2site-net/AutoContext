namespace AutoContext.Engine.Core.Features.McpTools.Format;

/// <summary>
/// Disk read-model for a single parameter spec under a tool's
/// <c>parameters</c> map in <c>mcp-tools-registry.json</c>. The map key is
/// the parameter name (camelCase); this record carries the value side.
/// Mirrors the <c>parameter</c> definition in
/// <c>mcp-tools-registry.schema.json</c>.
/// </summary>
/// <param name="Type">The JSON Schema type keyword the parameter accepts
/// (<c>string</c>, <c>number</c>, <c>boolean</c>, <c>array</c>, or
/// <c>object</c>).</param>
/// <param name="Description">The model-facing parameter description.</param>
/// <param name="Required">Whether the parameter is required;
/// <see langword="null"/> when the key is absent (treated as optional).</param>
internal sealed record JsonMcpToolsRegistryParameter(
    string? Type = null,
    string? Description = null,
    bool? Required = null);
