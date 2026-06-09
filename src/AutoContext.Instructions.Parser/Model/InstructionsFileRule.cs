namespace AutoContext.Instructions.Parser.Model;

using AutoContext.Instructions.Parser.Syntax;

/// <summary>
/// One instruction bullet — a list item of the form
/// <c>- [INST####] **Do**/**Don't** …</c> — together with its optional
/// <c>INST####</c> tag, which lets it be picked out on its own. A rule can span
/// several lines: blank and indented continuation lines belong to the bullet until
/// the next bullet or a line that is not indented.
/// </summary>
/// <param name="Id">The <c>INST####</c> tag, or <see langword="null"/> when the
/// bullet has no tag (and so cannot be picked out on its own).</param>
/// <param name="Text">The bullet text exactly as written, with trailing blank lines
/// trimmed.</param>
/// <param name="LineSpan">The lines the bullet covers, measured against the body
/// (with the frontmatter removed): from the bullet's first line to just past its
/// last non-blank line (trailing blank continuation lines trimmed).</param>
public sealed record InstructionsFileRule(
    string? Id,
    string Text,
    InstructionsFileLineSpan LineSpan);
