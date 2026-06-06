namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// The <c>disabled</c> arm of <see cref="JsonInstructionsGetResult"/>:
/// the requested file exists in the corpus but the user has muted it.
/// Identity-only by design — no description, body, or version — so a
/// language model cannot quote the muted rule back and route around
/// the user's choice.
/// </summary>
public sealed record JsonInstructionsGetDisabledResult : JsonInstructionsGetResult
{
    /// <summary>The corpus file name that was requested.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>File basename (the stable key).</summary>
    [JsonPropertyName("key")]
    public string? Key { get; init; }
}
