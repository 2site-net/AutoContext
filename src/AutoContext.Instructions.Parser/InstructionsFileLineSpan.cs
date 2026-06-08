namespace AutoContext.Instructions.Parser;

/// <summary>
/// A range of lines within some piece of text. Positions are zero-based:
/// <see cref="StartLine"/> is the first line and <see cref="EndLine"/> is one past
/// the last. A span may cover one line or several, so <see cref="LineCount"/> is
/// how many lines it touches. What the line numbers are measured against — the
/// whole file or the body with the frontmatter removed — depends on whatever
/// produced the span, so check the member that hands it to you.
/// </summary>
/// <param name="StartLine">The zero-based index of the first line covered.</param>
/// <param name="LineCount">The number of physical lines covered.</param>
public readonly record struct InstructionsFileLineSpan(int StartLine, int LineCount)
{
    /// <summary>The exclusive end line — one past the last line covered.</summary>
    public int EndLine => StartLine + LineCount;
}
