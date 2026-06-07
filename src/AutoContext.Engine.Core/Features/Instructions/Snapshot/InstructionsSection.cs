namespace AutoContext.Engine.Core.Features.Instructions.Snapshot;

/// <summary>
/// Immutable section heading of a bundled instruction file: the
/// <c>##</c>/<c>###</c> heading text and the GitHub-style
/// <see cref="Anchor"/> a deep link targets. The heading level is not
/// stored — it is trivially <c>Parent is not null ? 3 : 2</c>. Pure
/// data, projected from the build-time manifest side-car.
/// </summary>
internal sealed record InstructionsSection
{
    /// <summary>
    /// The trimmed heading text, without the leading hashes.
    /// </summary>
    public required string Heading { get; init; }

    /// <summary>
    /// The GitHub-slug anchor; a <c>###</c> anchor is prefixed with its
    /// parent <c>##</c> slug.
    /// </summary>
    public required string Anchor { get; init; }

    /// <summary>
    /// The parent <c>##</c> heading text for a <c>###</c> section, or
    /// <see langword="null"/>.
    /// </summary>
    public string? Parent { get; init; }
}
