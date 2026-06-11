namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// One <c>##</c>/<c>###</c> heading in an instructions file's section
/// index, in document order. The shape intentionally matches the
/// build-time <c>instructions-manifest.json</c> generator output
/// — <c>heading</c>, <c>anchor</c>, <c>parent?</c>, no <c>level</c>;
/// the parent chain carries hierarchy.
/// </summary>
public sealed record JsonInstructionsSection
{
    /// <summary>The verbatim heading text.</summary>
    [JsonPropertyName("heading")]
    public string? Heading { get; init; }

    /// <summary>The GitHub-slug anchor for the heading.</summary>
    [JsonPropertyName("anchor")]
    public string? Anchor { get; init; }

    /// <summary>
    /// The parent <c>##</c> heading name for a <c>###</c> subsection,
    /// or <see langword="null"/> for a top-level <c>##</c> heading.
    /// </summary>
    [JsonPropertyName("parent")]
    public string? Parent { get; init; }
}
