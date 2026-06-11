namespace AutoContext.Engine.Protocol.Messages.Config;

using System.Text.Json.Serialization;

/// <summary>
/// Parameters of the <see cref="ConfigMethods.ToggleFile"/> request:
/// the instructions file whose whole-file disabled state should be
/// flipped. Toggling an untracked file disables it; toggling a
/// disabled file re-enables it.
/// </summary>
public sealed record JsonConfigToggleFileParams
{
    /// <summary>
    /// The instructions file name to toggle, matching
    /// <see cref="JsonConfigInstructionsFile.Name"/>. Required.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}
