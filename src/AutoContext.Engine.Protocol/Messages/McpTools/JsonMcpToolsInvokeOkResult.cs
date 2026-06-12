namespace AutoContext.Engine.Protocol.Messages.McpTools;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// The <c>ok</c> arm of <see cref="JsonMcpToolsInvokeResult"/>: the
/// tool was dispatched and ran to completion without reporting failure.
/// </summary>
public sealed record JsonMcpToolsInvokeOkResult : JsonMcpToolsInvokeResult
{
    /// <summary>The tool name that was invoked.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// The worker's content blocks, matching MCP's
    /// <c>CallToolResult.content</c> array verbatim so the pipe and
    /// MCP/stdio surfaces serialise byte-identically (<c>design §
    /// P1</c>). Carried as raw <see cref="JsonElement"/> blocks to pass
    /// the worker payload through untouched.
    /// </summary>
    [JsonPropertyName("content")]
    public IReadOnlyList<JsonElement> Content { get; init; } = [];

    /// <summary>
    /// MCP <c>isError</c> parity flag — <see langword="false"/> or
    /// omitted on this arm. Present so the serialised <c>content</c>
    /// envelope matches the MCP/stdio shape.
    /// </summary>
    [JsonPropertyName("isError")]
    public bool? IsError { get; init; }
}
