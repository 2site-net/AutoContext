namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// The <c>not-found</c> arm of
/// <see cref="JsonInstructionsGetResult"/>: the requested name was
/// never in the corpus at all — strictly distinct from
/// <see cref="JsonInstructionsGetDisabledResult"/> (no user policy
/// involved).
/// </summary>
public sealed record JsonInstructionsGetNotFoundResult : JsonInstructionsGetResult
{
    /// <summary>The corpus file name that was requested.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}
