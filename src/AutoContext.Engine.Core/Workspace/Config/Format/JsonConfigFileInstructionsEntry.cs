namespace AutoContext.Engine.Core.Workspace.Config.Format;

using System.Text.Json.Serialization;

/// <summary>
/// A single entry under the <c>instructions</c> map of
/// <c>.autocontext.json</c>, holding one instruction file's state. The
/// parameter order (<c>version</c>, <c>enabled</c>,
/// <c>disabledInstructions</c>) is the on-disk key order; keep it
/// stable so saved files stay byte-for-byte stable.
/// </summary>
/// <param name="Version">The MAJOR.MINOR instruction-file version the
/// disabled-rule ids were recorded against, so a later version change
/// can tell whether those ids are still current.</param>
/// <param name="Enabled"><see langword="false"/> when the whole
/// instruction file is disabled. Only ever <see langword="false"/>
/// when present; absent means enabled.</param>
/// <param name="DisabledInstructions">Ids of individual rules the
/// user has turned off. Absent (not an empty array) when no rule is
/// disabled.</param>
internal sealed record JsonConfigFileInstructionsEntry(
    [property: JsonPropertyName("version")] string? Version = null,
    [property: JsonPropertyName("enabled")] bool? Enabled = null,
    [property: JsonPropertyName("disabledInstructions")] IReadOnlyList<string>? DisabledInstructions = null);
