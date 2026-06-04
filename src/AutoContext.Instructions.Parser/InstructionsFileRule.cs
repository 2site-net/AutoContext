namespace AutoContext.Instructions.Parser;

/// <summary>
/// One actionable instruction bullet — a list item of the form
/// <c>- [INST####] **Do**/**Don't** …</c> — together with the optional
/// <c>INST####</c> tag that makes it individually filterable. A rule may span
/// multiple lines: blank and indented continuation lines belong to the bullet
/// until the next bullet or a non-indented line.
/// </summary>
/// <param name="Id">The <c>INST####</c> tag, or <see langword="null"/> when the
/// bullet carries no tag (and is therefore unfilterable).</param>
/// <param name="Text">The verbatim bullet text, trailing blank lines trimmed.</param>
/// <param name="StartLine">The zero-based line index of the bullet's first line
/// within the normalised body.</param>
/// <param name="EndLine">The zero-based line index of the bullet's last
/// non-blank line within the normalised body.</param>
public sealed record InstructionsFileRule(
    string? Id,
    string Text,
    int StartLine,
    int EndLine);
