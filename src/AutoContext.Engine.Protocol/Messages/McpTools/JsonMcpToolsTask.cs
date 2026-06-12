namespace AutoContext.Engine.Protocol.Messages.McpTools;

using System.Text.Json.Serialization;

/// <summary>
/// One MCP task carried on <see cref="JsonMcpToolsListRow.Tasks"/> — a
/// unit of work the parent tool dispatches, with its own engine-resolved
/// disabled state. A task may be disabled independently of its parent
/// tool, mirroring the per-tool / per-task split in
/// <c>.autocontext.json</c>.
/// </summary>
public sealed record JsonMcpToolsTask
{
    /// <summary>The MCP task name (unique within its parent tool).</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Engine-resolved disabled state against <c>.autocontext.json</c>,
    /// independent of the parent tool's state.
    /// </summary>
    [JsonPropertyName("disabled")]
    public bool Disabled { get; init; }
}
