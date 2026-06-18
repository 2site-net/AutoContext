namespace AutoContext.Engine.Protocol.Messages.McpTools;

using System.Text.Json.Serialization;

/// <summary>
/// The <c>schema-error</c> arm of
/// <see cref="JsonMcpToolsInvokeResult"/>: the supplied
/// <see cref="JsonMcpToolsInvokeParams.Arguments"/> failed validation
/// against the tool's <c>inputSchema</c>, so the engine never
/// dispatched. The engine runs the same validation the MCP/stdio path
/// performs, sharing one validator to avoid drift.
/// </summary>
public sealed record JsonMcpToolsInvokeSchemaErrorResult : JsonMcpToolsInvokeResult
{
    /// <summary>The tool name whose arguments failed validation.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>The argument-validation failures, one per offending path.</summary>
    [JsonPropertyName("errors")]
    public IReadOnlyList<JsonMcpToolsSchemaError> Errors { get; init; } = [];
}
