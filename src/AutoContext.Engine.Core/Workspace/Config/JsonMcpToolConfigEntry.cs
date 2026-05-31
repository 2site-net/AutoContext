namespace AutoContext.Engine.Core.Workspace.Config;

using System.Text.Json.Serialization;

/// <summary>
/// The object form of an <c>mcpTools</c> entry in
/// <c>.autocontext.json</c>. An <c>mcpTools</c> value can also be the
/// bare <c>false</c> shorthand; <see cref="JsonMcpToolConfigValue"/>
/// covers both forms. The parameter order (<c>enabled</c>,
/// <c>version</c>, <c>disabledTasks</c>) is the on-disk key order;
/// keep it stable so saved files stay byte-for-byte stable.
/// </summary>
/// <param name="Enabled"><see langword="false"/> when the tool is
/// disabled while still carrying other state (e.g.
/// <see cref="DisabledTasks"/>). Only ever <see langword="false"/>
/// when present; absent means the tool itself is enabled.</param>
/// <param name="Version">MAJOR.MINOR version the entry was captured
/// against.</param>
/// <param name="DisabledTasks">Names of individual tasks the user has
/// turned off. Tasks are independent of the parent tool's enabled
/// state. Absent (not an empty array) when no task is disabled.</param>
internal sealed record JsonMcpToolConfigEntry(
    [property: JsonPropertyName("enabled")] bool? Enabled = null,
    [property: JsonPropertyName("version")] string? Version = null,
    [property: JsonPropertyName("disabledTasks")] IReadOnlyList<string>? DisabledTasks = null);
