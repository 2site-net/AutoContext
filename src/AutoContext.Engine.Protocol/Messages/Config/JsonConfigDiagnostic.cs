namespace AutoContext.Engine.Protocol.Messages.Config;

using System.Text.Json.Serialization;

/// <summary>
/// Wire shape of the config's diagnostic toggles, carried on
/// <see cref="JsonConfigSnapshot.Diagnostic"/>.
/// </summary>
public sealed record JsonConfigDiagnostic
{
    /// <summary>
    /// Whether the engine should warn when an instruction rule has no
    /// stable id. <see langword="null"/> means "unset" (engine
    /// default applies).
    /// </summary>
    [JsonPropertyName("warnOnMissingId")]
    public bool? WarnOnMissingId { get; init; }
}
