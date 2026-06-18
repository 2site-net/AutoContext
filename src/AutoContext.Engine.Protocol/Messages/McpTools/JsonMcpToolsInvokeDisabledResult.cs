namespace AutoContext.Engine.Protocol.Messages.McpTools;

using System.Text.Json.Serialization;

/// <summary>
/// The <c>disabled</c> arm of <see cref="JsonMcpToolsInvokeResult"/>:
/// the tool exists in the catalog but the user has muted it in
/// <c>.autocontext.json</c>, so the engine refused to dispatch. Identity
/// only — no content, no schema — so a model cannot route around the
/// user's mute (<c>design § P2</c>).
/// </summary>
public sealed record JsonMcpToolsInvokeDisabledResult : JsonMcpToolsInvokeResult
{
    /// <summary>The tool name that was requested.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}
