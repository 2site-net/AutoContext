namespace AutoContext.Engine.Protocol.Messages.McpTools;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Parameters of the <see cref="McpToolsMethods.Invoke"/> request — the
/// pipe-side equivalent of MCP's <c>tools/call</c> arguments.
/// </summary>
public sealed record JsonMcpToolsInvokeParams
{
    /// <summary>The MCP tool name to invoke. Required.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// The tool arguments object, passed through verbatim to the
    /// engine's shared schema validator and worker dispatch. Carried as
    /// a raw <see cref="JsonElement"/> so the pipe and MCP/stdio paths
    /// see byte-identical arguments. <see langword="null"/> when the
    /// tool takes no arguments.
    /// </summary>
    [JsonPropertyName("arguments")]
    public JsonElement? Arguments { get; init; }
}
