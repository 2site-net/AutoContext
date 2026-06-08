namespace AutoContext.Instructions.Parser;

/// <summary>
/// A physical-line range within the file. Coordinates are zero-based:
/// <see cref="StartLine"/> is the first line covered and <see cref="EndLine"/> is
/// exclusive. A span may cover part of a line, exactly one line, or several lines,
/// so <see cref="LineCount"/> is the number of physical lines the span touches.
/// </summary>
/// <param name="StartLine">The zero-based index of the first line covered.</param>
/// <param name="LineCount">The number of physical lines covered.</param>
public readonly record struct InstructionsFileLineSpan(int StartLine, int LineCount)
{
    /// <summary>The exclusive end line — one past the last line covered.</summary>
    public int EndLine => StartLine + LineCount;
}
