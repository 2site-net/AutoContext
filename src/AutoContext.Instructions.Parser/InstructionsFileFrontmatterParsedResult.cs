namespace AutoContext.Instructions.Parser;

/// <summary>
/// The subset of an instructions file's leading YAML frontmatter the parser
/// reads. Every field is optional at the parse layer: a file may carry no
/// frontmatter block at all, in which case all fields are <see langword="null"/>.
/// Presence and shape validation (required <c>name</c>, <c>&lt;key&gt; (vX.Y.Z)</c>
/// form, non-empty <c>description</c>) is a consumer concern, not the parser's.
/// </summary>
/// <param name="Name">The raw <c>name</c> field (expected
/// <c>&lt;key&gt; (vX.Y.Z)</c>), or <see langword="null"/> when absent.</param>
/// <param name="Description">The raw <c>description</c> field, or
/// <see langword="null"/> when absent.</param>
/// <param name="ApplyTo">The parsed <c>applyTo</c> glob expression, or
/// <see langword="null"/> when the file declares no <c>applyTo</c> (e.g. an
/// always-attached file).</param>
/// <param name="Version">The semantic version extracted from the
/// <c>(vX.Y.Z)</c> suffix of <paramref name="Name"/>, or
/// <see langword="null"/> when <paramref name="Name"/> carries no such suffix.</param>
public sealed record InstructionsFileFrontmatterParsedResult(
    string? Name,
    string? Description,
    FrontmatterApplyToParsedResult? ApplyTo,
    string? Version);
