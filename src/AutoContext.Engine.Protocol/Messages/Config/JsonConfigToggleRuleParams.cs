namespace AutoContext.Engine.Protocol.Messages.Config;

using System.Text.Json.Serialization;

/// <summary>
/// Parameters of the <see cref="ConfigMethods.ToggleRule"/> request:
/// the single rule, identified by its owning instructions file and
/// rule id, whose disabled state should be flipped. Toggling an
/// enabled rule disables it; toggling a disabled rule re-enables it.
/// </summary>
public sealed record JsonConfigToggleRuleParams
{
    /// <summary>
    /// The instructions file name that owns the rule, matching
    /// <see cref="JsonConfigInstructionsFile.Name"/>. Required.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// The rule id to toggle within the file, matching
    /// <see cref="JsonConfigInstructionsRule.Id"/>. Required.
    /// </summary>
    [JsonPropertyName("ruleId")]
    public string? RuleId { get; init; }
}
