namespace AutoContext.Engine.Core.Workspace.Config.Format;

using System.Text.Json.Serialization;

/// <summary>
/// A single entry under the <c>instructions</c> map of
/// <c>.autocontext.json</c>, holding one instruction file's state. The
/// parameter order (<c>version</c>, <c>disabled</c>,
/// <c>disabledRules</c>) is the on-disk key order; keep it stable so
/// saved files stay byte-for-byte stable.
/// </summary>
/// <param name="Version">The MAJOR.MINOR instruction-file version the
/// disabled-rule ids were recorded against, so a later version change
/// can tell whether those ids are still current.</param>
/// <param name="Disabled"><see langword="true"/> when the whole
/// instruction file is disabled. Only ever <see langword="true"/>
/// when present; absent means enabled.</param>
/// <param name="DisabledRules">Ids of individual rules the user has
/// turned off. Absent (not an empty array) when no rule is
/// disabled.</param>
internal sealed record JsonConfigFileInstructionsEntry(
    [property: JsonPropertyName("version")] string? Version = null,
    [property: JsonPropertyName("disabled")] bool? Disabled = null,
    [property: JsonPropertyName("disabledRules")] IReadOnlyList<string>? DisabledRules = null);
