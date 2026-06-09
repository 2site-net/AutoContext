namespace AutoContext.Instructions.Parser.Syntax;

/// <summary>
/// One piece of an instructions file that <see cref="InstructionsFileSyntaxParser"/>
/// found and labelled. A span is not the same as a line: it may cover part of a
/// line, a whole line, or several lines. A large block span (a frontmatter block,
/// a rule bullet) can hold smaller token spans inside it (keys, values, tags,
/// references), so in <see cref="InstructionsFileSpanEmitLevel.Full"/> mode the
/// spans you get back can overlap.
/// </summary>
/// <param name="Text">The exact text the span covers, as a slice of the loaded
/// file (no copy is made; it is the same characters as the substring at
/// <see cref="TextSpan"/>).</param>
/// <param name="Kind">What kind of thing the span is.</param>
/// <param name="TextSpan">The whole-file character range the span covers.</param>
/// <param name="LineSpan">The physical-line range the span covers.</param>
public sealed record InstructionsFileSyntaxSpan(
    ReadOnlyMemory<char> Text,
    InstructionsFileSpanKind Kind,
    InstructionsFileTextSpan TextSpan,
    InstructionsFileLineSpan LineSpan)
{
    /// <summary>
    /// Compares this span with <paramref name="other"/> by value. The record's
    /// generated equality is replaced because <see cref="Text"/> is a
    /// <see cref="ReadOnlyMemory{T}"/>, which by default compares the underlying
    /// buffer and slice bounds rather than the actual characters. This version
    /// compares <see cref="Text"/> character by character, so two spans with the
    /// same content are equal even when they were sliced from different buffers.
    /// </summary>
    /// <param name="other">The span to compare against.</param>
    /// <returns><see langword="true"/> if the spans are equal by value.</returns>
    public bool Equals(InstructionsFileSyntaxSpan? other)
        => other is not null
            && Kind == other.Kind
            && TextSpan == other.TextSpan
            && LineSpan == other.LineSpan
            && Text.Span.SequenceEqual(other.Text.Span);

    /// <summary>
    /// Computes a hash code that matches <see cref="Equals(InstructionsFileSyntaxSpan)"/>,
    /// hashing the <see cref="Text"/> characters rather than the memory reference.
    /// </summary>
    /// <returns>The content-based hash code.</returns>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(Kind);
        hash.Add(TextSpan);
        hash.Add(LineSpan);
        hash.Add(string.GetHashCode(Text.Span, StringComparison.Ordinal));

        return hash.ToHashCode();
    }
}
