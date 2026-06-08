namespace AutoContext.Instructions.Parser;

/// <summary>
/// A physical-line range within some unit of text. Coordinates are zero-based:
/// <see cref="StartLine"/> is the first line covered and <see cref="EndLine"/> is
/// exclusive. A span may cover exactly one line or several lines, so
/// <see cref="LineCount"/> is the number of physical lines the span touches. The
/// coordinate system the indices count in — whole-file text or the
/// frontmatter-stripped body — is defined by whatever produces the span; consult
/// the member that exposes it.
/// </summary>
/// <param name="StartLine">The zero-based index of the first line covered.</param>
/// <param name="LineCount">The number of physical lines covered.</param>
public readonly record struct InstructionsFileLineSpan(int StartLine, int LineCount)
{
    /// <summary>The exclusive end line — one past the last line covered.</summary>
    public int EndLine => StartLine + LineCount;
}
