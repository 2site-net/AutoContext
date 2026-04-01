namespace SharpPilot.WorkspaceServer.Features.EditorConfig.Protocol;

using System.Text.Json.Serialization;

/// <summary>
/// Mode assigned to each MCP tool by the workspace service.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<McpToolMode>))]
internal enum McpToolMode
{
    /// <summary>Tool is enabled — execute normally.</summary>
    Run,

    /// <summary>Tool is disabled but has EditorConfig keys — run EditorConfig-only floor check.</summary>
    [JsonStringEnumMemberName("editorconfig-only")]
    EditorConfigOnly,

    /// <summary>Tool is disabled and has no EditorConfig keys — skip entirely.</summary>
    Skip,
}
