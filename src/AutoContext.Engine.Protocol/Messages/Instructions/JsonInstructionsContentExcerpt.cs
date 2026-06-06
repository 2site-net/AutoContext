namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// One excerpt of a <see cref="JsonInstructionsContentHit"/> — a
/// matched window of projected body text with the section it falls in.
/// </summary>
public sealed record JsonInstructionsContentExcerpt
{
    /// <summary>
    /// Anchor of the section the match falls in, for chaining into
    /// <see cref="InstructionsMethods.Get"/> section slicing. Empty
    /// when the match precedes the first heading.
    /// </summary>
    [JsonPropertyName("anchor")]
    public string? Anchor { get; init; }

    /// <summary>The trimmed excerpt text around the match.</summary>
    [JsonPropertyName("snippet")]
    public string? Snippet { get; init; }

    /// <summary>
    /// One-based body line of the match, or <see langword="null"/>
    /// when not tracked.
    /// </summary>
    [JsonPropertyName("line")]
    public int? Line { get; init; }
}
