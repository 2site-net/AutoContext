namespace AutoContext.Instructions.Manifest.Generator;

using System.Text.Json.Serialization;

/// <summary>
/// One section of an instruction file's body as it appears in the catalogue-only
/// <c>instructions-files-metadata.json</c> index: the <c>##</c>/<c>###</c> heading
/// text and the GitHub-style <see cref="Anchor"/> a deep link targets. The heading
/// level is intentionally not stored — it is trivially <c>parent is not null ? 3 : 2</c>
/// — and neither are the body-relative offsets the parser carries, which serve
/// runtime slicing rather than the wire index.
/// </summary>
internal sealed class InstructionsMetadataSection(
    string heading,
    string anchor,
    string? parent)
{
    /// <summary>Gets the GitHub-slug anchor; a <c>###</c> anchor is prefixed with its parent <c>##</c> slug.</summary>
    [JsonPropertyOrder(1)]
    public string Anchor { get; } = anchor;

    /// <summary>Gets the trimmed heading text, without the leading hashes.</summary>
    [JsonPropertyOrder(0)]
    public string Heading { get; } = heading;

    /// <summary>Gets the parent <c>##</c> heading text for a <c>###</c> section, or <see langword="null"/>.</summary>
    [JsonPropertyOrder(2)]
    public string? Parent { get; } = parent;
}
