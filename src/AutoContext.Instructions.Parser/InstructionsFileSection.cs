namespace AutoContext.Instructions.Parser;

/// <summary>
/// One <c>##</c> or <c>###</c> heading in an instruction file's body, with the
/// GitHub-style anchor a deep link would target and the body-relative character
/// span the section covers. Deeper headings (<c>####</c>+) and the document title
/// (<c>#</c>) are not sections; headings inside fenced code blocks are ignored.
/// </summary>
/// <param name="Heading">The trimmed heading text, without the leading hashes.</param>
/// <param name="Level">The heading level: <c>2</c> for <c>##</c>, <c>3</c> for
/// <c>###</c>.</param>
/// <param name="Anchor">The GitHub-slug anchor; a <c>###</c> anchor is prefixed
/// with its parent <c>##</c> slug for in-file uniqueness.</param>
/// <param name="Parent">The text of the nearest preceding <c>##</c> heading for
/// a <c>###</c> section; <see langword="null"/> for a <c>##</c> section.</param>
/// <param name="CharStart">The offset of the heading line into the normalised
/// (frontmatter-stripped) body.</param>
/// <param name="CharEnd">The exclusive offset at which the section ends — the
/// start of the next heading of equal-or-shallower level, or the body length.</param>
public sealed record InstructionsFileSection(
    string Heading,
    int Level,
    string Anchor,
    string? Parent,
    int CharStart,
    int CharEnd);
