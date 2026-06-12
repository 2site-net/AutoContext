namespace AutoContext.Engine.Protocol.Messages.McpTools;

using System.Text.Json.Serialization;

/// <summary>
/// One identity row of the <see cref="McpToolsMethods.List"/> listing —
/// a single MCP tool projected from the embedded
/// <c>mcp-tools-registry.json</c>, with the engine-resolved disabled
/// state layered on per request. Input schemas are deliberately absent
/// (schema exposure on the pipe is a later meta-discovery concern); the
/// shape leaves room for the forward-noted <see cref="Categories"/> /
/// <see cref="Description"/> / <see cref="Key"/> metadata so the
/// discovery siblings can land additively per <c>design § RPC surface
/// (McpTools.*)</c>.
/// </summary>
public sealed record JsonMcpToolsListRow
{
    /// <summary>
    /// Stable per-tool key. Equals <see cref="Name"/> today; carried
    /// separately so future metadata keying stays additive.
    /// </summary>
    [JsonPropertyName("key")]
    public string? Key { get; init; }

    /// <summary>The MCP tool name (snake_case, unique across the catalog).</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>The tool description advertised to MCP clients.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Category-membership names the tool belongs to, leaf-last. Empty
    /// when the tool is uncategorized. Backs the
    /// <c>category → tools</c> index a future
    /// <c>Discovery.RouteForPrompt</c> inverts.
    /// </summary>
    [JsonPropertyName("categories")]
    public IReadOnlyList<string> Categories { get; init; } = [];

    /// <summary>
    /// The MCP tasks this tool dispatches when invoked, each carrying
    /// its own engine-resolved disabled state. Empty when the tool
    /// declares no tasks.
    /// </summary>
    [JsonPropertyName("tasks")]
    public IReadOnlyList<JsonMcpToolsTask> Tasks { get; init; } = [];

    /// <summary>
    /// Engine-resolved disabled state against <c>.autocontext.json</c>.
    /// Disabled rows still appear in <see cref="McpToolsMethods.List"/>
    /// so the tree view can render the toggle UI.
    /// </summary>
    [JsonPropertyName("disabled")]
    public bool Disabled { get; init; }
}
