namespace AutoContext.Instructions.Parser;

/// <summary>
/// A character range within some unit of decoded text. Coordinates are zero-based:
/// <see cref="StartIndex"/> is an offset into the text the producer addresses and
/// <see cref="EndIndex"/> is exclusive. The coordinate system the offsets count in
/// — whole-file text or the frontmatter-stripped body — is defined by whatever
/// produces the span; consult the member that exposes it. No newline normalisation
/// is implied, so a <c>CRLF</c> pair counts as two characters.
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
