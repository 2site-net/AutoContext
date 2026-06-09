namespace AutoContext.Instructions.Parser.Model;

/// <summary>
/// The parts of an instructions file's frontmatter that the parser reads. Every
/// field is optional here: a file may have no frontmatter at all, in which case
/// they are all <see langword="null"/>. Checking that the fields are present and
/// well-formed (a required <c>name</c> in <c>&lt;key&gt; (vX.Y.Z)</c> form, a
/// non-empty <c>description</c>) is up to the consumer, not the parser.
/// </summary>
/// <param name="RawValue">The text between the leading <c>---</c> fences exactly as
/// written — not the fences or the newlines around them — or the empty string when
/// the file has no frontmatter.</param>
/// <param name="Name">The <c>name</c> field as written (expected to be
/// <c>&lt;key&gt; (vX.Y.Z)</c>), or <see langword="null"/> when missing.</param>
/// <param name="Description">The <c>description</c> field as written, or
/// <see langword="null"/> when missing.</param>
/// <param name="ApplyTo">The parsed <c>applyTo</c> glob, or <see langword="null"/>
/// when the file has no <c>applyTo</c> (for example, a file that always
/// applies).</param>
/// <param name="Version">The version taken from the <c>(vX.Y.Z)</c> suffix of
/// <paramref name="Name"/>, or <see langword="null"/> when <paramref name="Name"/>
/// has no such suffix.</param>
public sealed record InstructionsFileFrontmatter(
    string RawValue,
    string? Name,
    string? Description,
    FrontmatterApplyTo? ApplyTo,
    string? Version);
