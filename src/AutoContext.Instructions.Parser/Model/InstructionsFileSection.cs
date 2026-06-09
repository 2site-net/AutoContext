namespace AutoContext.Instructions.Parser.Model;

using AutoContext.Instructions.Parser.Syntax;

/// <summary>
/// One <c>##</c> or <c>###</c> heading in an instructions file's body, with the
/// GitHub-style anchor a link would jump to and the character range the section
/// covers (measured against the body). Deeper headings (<c>####</c>+) and the
/// document title (<c>#</c>) are not sections; headings inside fenced code blocks
/// are ignored.
/// </summary>
/// <param name="Heading">The trimmed heading text, without the leading hashes.</param>
/// <param name="Level">The heading level: <c>2</c> for <c>##</c>, <c>3</c> for
/// <c>###</c>.</param>
/// <param name="Anchor">The GitHub-slug anchor; a <c>###</c> anchor is prefixed
/// with its parent <c>##</c> slug for in-file uniqueness.</param>
/// <param name="Parent">The text of the nearest preceding <c>##</c> heading for
/// a <c>###</c> section; <see langword="null"/> for a <c>##</c> section.</param>
/// <param name="TextSpan">The character range the section covers, measured against
/// the body (with the frontmatter removed): from the start of the heading line to
/// where the section ends — the start of the next heading at the same level or a
/// shallower one (fewer <c>#</c>), or the end of the body.</param>
public sealed record InstructionsFileSection(
    string Heading,
    int Level,
    string Anchor,
    string? Parent,
    InstructionsFileTextSpan TextSpan);
