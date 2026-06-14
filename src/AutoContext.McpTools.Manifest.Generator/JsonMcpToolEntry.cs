namespace AutoContext.McpTools.Manifest.Generator;

using System.Text.Json.Serialization;

/// <summary>
/// One tool row of the build-generated <c>mcp-tools.json</c> catalog, projected
/// from a registry tool. It carries the wire-visible <see cref="Name"/>,
/// <see cref="Description"/>, and <see cref="Tasks"/>; the registry's input
/// <c>parameters</c> are dropped because the <c>McpTools.List</c> wire shape omits
/// input schemas, and the per-request <c>disabled</c> state and forward-noted
/// category membership are layered by the engine rather than baked into the file.
/// </summary>
internal sealed class JsonMcpToolEntry(string name, string description, IReadOnlyList<JsonMcpTaskEntry> tasks)
{
    /// <summary>Gets the tool description advertised to MCP clients.</summary>
    [JsonPropertyOrder(1)]
    public string Description { get; } = description;

    /// <summary>Gets the MCP tool name (snake_case, unique across the catalog).</summary>
    [JsonPropertyOrder(0)]
    public string Name { get; } = name;

    /// <summary>Gets the tasks this tool dispatches when invoked, in registry order.</summary>
    [JsonPropertyOrder(2)]
    public IReadOnlyList<JsonMcpTaskEntry> Tasks { get; } = tasks;
}
