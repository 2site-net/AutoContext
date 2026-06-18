namespace AutoContext.Engine.Protocol.Messages.McpTools;

using System.Text.Json.Serialization;

/// <summary>
/// One argument-validation failure carried on
/// <see cref="JsonMcpToolsInvokeSchemaErrorResult.Errors"/> — the
/// offending argument path paired with a human-readable message.
/// </summary>
public sealed record JsonMcpToolsSchemaError
{
    /// <summary>
    /// JSON path of the offending argument (e.g. <c>content</c> or
    /// <c>options/depth</c>), or empty for a document-level failure.
    /// </summary>
    [JsonPropertyName("path")]
    public string? Path { get; init; }

    /// <summary>Human-readable description of the validation failure.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }
}
