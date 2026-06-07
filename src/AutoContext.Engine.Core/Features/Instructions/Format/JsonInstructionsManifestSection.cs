namespace AutoContext.Engine.Core.Features.Instructions.Format;

/// <summary>
/// One <c>##</c>/<c>###</c> section row of a
/// <see cref="JsonInstructionsManifestEntry"/>. Mirrors the generator's
/// <c>JsonInstructionsManifestSection</c> writer.
/// </summary>
/// <param name="Heading">The trimmed heading text, without the leading
/// hashes.</param>
/// <param name="Anchor">The GitHub-slug anchor; a <c>###</c> anchor is
/// prefixed with its parent <c>##</c> slug.</param>
/// <param name="Parent">The parent <c>##</c> heading text for a
/// <c>###</c> section, or <see langword="null"/>.</param>
internal sealed record JsonInstructionsManifestSection(
    string? Heading = null,
    string? Anchor = null,
    string? Parent = null);
