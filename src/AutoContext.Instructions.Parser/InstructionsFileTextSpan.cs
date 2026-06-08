namespace AutoContext.Instructions.Parser;

/// <summary>
/// A character range within the decoded file text. Coordinates are whole-file and
/// zero-based: <see cref="StartIndex"/> is an offset into the decoded UTF-16 text
/// with no frontmatter stripping or newline normalisation applied, so a
/// <c>CRLF</c> pair counts as two characters. <see cref="EndIndex"/> is exclusive.
/// </summary>
/// <param name="StartIndex">The zero-based index of the first character.</param>
/// <param name="Length">The number of characters covered.</param>
public readonly record struct InstructionsFileTextSpan(int StartIndex, int Length)
{
    /// <summary>The exclusive end index — one past the last character covered.</summary>
    public int EndIndex => StartIndex + Length;

    /// <summary>The range as a <see cref="System.Range"/>, from
    /// <see cref="StartIndex"/> to the exclusive <see cref="EndIndex"/>.</summary>
    public Range Range => StartIndex..EndIndex;
}
