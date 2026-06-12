namespace AutoContext.Engine.Protocol.Messages.McpTools;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// The <c>tool-error</c> arm of <see cref="JsonMcpToolsInvokeResult"/>:
/// the tool was dispatched and ran, but reported failure. Distinct from
/// <c>disabled</c> / <c>not-found</c> (where the engine refused to
/// dispatch at all), same correctness rationale as
/// <c>Instructions.Get</c>'s envelope split.
/// </summary>
public sealed record JsonMcpToolsInvokeToolErrorResult : JsonMcpToolsInvokeResult
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
    /// MCP <c>isError</c> parity flag — always <see langword="true"/>
    /// on this arm.
    /// </summary>
    [JsonPropertyName("isError")]
    public bool IsError { get; init; } = true;
}
