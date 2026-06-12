namespace AutoContext.Engine.Protocol.Messages.McpTools;

using System.Text.Json.Serialization;

/// <summary>
/// The <c>not-found</c> arm of <see cref="JsonMcpToolsInvokeResult"/>:
/// the requested tool name was never in the catalog (no user policy
/// involved). Strictly distinct from <c>disabled</c> per
/// <c>design § P2</c>. Identity only.
/// </summary>
public sealed record JsonMcpToolsInvokeNotFoundResult : JsonMcpToolsInvokeResult
{
    /// <summary>The tool name that was requested.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}
