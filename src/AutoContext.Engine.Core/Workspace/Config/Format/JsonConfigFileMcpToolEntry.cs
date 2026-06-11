namespace AutoContext.Engine.Core.Workspace.Config.Format;

using System.Text.Json.Serialization;

/// <summary>
/// A single entry under the <c>mcpTools</c> map of
/// <c>.autocontext.json</c>, holding one MCP tool's state. The
/// parameter order (<c>version</c>, <c>disabled</c>,
/// <c>disabledTasks</c>) is the on-disk key order; keep it stable so
/// saved files stay byte-for-byte stable.
/// </summary>
/// <param name="Version">MAJOR.MINOR version the entry was captured
/// against.</param>
/// <param name="Disabled"><see langword="true"/> when the whole tool
/// is disabled. Only ever <see langword="true"/> when present; absent
/// means the tool itself is enabled.</param>
/// <param name="DisabledTasks">Names of individual tasks the user has
/// turned off. Tasks are independent of the parent tool's enabled
/// state. Absent (not an empty array) when no task is disabled.</param>
internal sealed record JsonConfigFileMcpToolEntry(
    [property: JsonPropertyName("version")] string? Version = null,
    [property: JsonPropertyName("disabled")] bool? Disabled = null,
    [property: JsonPropertyName("disabledTasks")] IReadOnlyList<string>? DisabledTasks = null);
