namespace AutoContext.Instructions.Parser;

/// <summary>
/// A range of characters within some piece of text. Positions are zero-based:
/// <see cref="StartIndex"/> is the first character and <see cref="EndIndex"/> is
/// one past the last. What the positions are measured against — the whole file or
/// the body with the frontmatter removed — depends on whatever produced the span,
/// so check the member that hands it to you. Nothing is done to line breaks, so a
/// <c>CRLF</c> pair counts as two characters.
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
