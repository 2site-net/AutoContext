namespace AutoContext.Engine.Core.Workspace.Config.Format;

using System.Text.Json.Serialization;

/// <summary>
/// Optional <c>diagnostic</c> block of <c>.autocontext.json</c>.
/// Carried through verbatim on load and save so the engine never
/// drops a user's diagnostic preferences when it rewrites the file.
/// </summary>
/// <param name="WarnOnMissingId">When <see langword="false"/>,
/// suppresses the warning emitted for instruction rules that lack an
/// <c>id</c>. Absent when the user never set it.</param>
internal sealed record JsonConfigFileDiagnostic(
    [property: JsonPropertyName("warnOnMissingId")] bool? WarnOnMissingId = null);
