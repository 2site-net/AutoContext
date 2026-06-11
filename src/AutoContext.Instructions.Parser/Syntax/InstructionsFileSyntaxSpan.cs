namespace AutoContext.Instructions.Parser.Syntax;

using System.Runtime.InteropServices;

/// <summary>
/// One piece of an instructions file that <see cref="InstructionsFileSyntaxParser"/>
/// found and labelled. A span is not the same as a line: it may cover part of a
/// line, a whole line, or several lines. A large block span (a frontmatter block,
/// a rule bullet) can hold smaller token spans inside it (keys, values, tags,
/// references), so in <see cref="InstructionsFileSpanEmitLevel.Full"/> mode the
/// spans you get back can overlap.
/// <para>
/// A span carries its position twice. <see cref="TextSpan"/> and
/// <see cref="LineSpan"/> count from the start of the whole file, frontmatter
/// included, which is what a physical-file consumer (the manifest generator) wants.
/// <see cref="Offset"/> counts from the start of the span's own region instead: a
/// body span measures from the start of the body, as if the frontmatter were not
/// there, so a body consumer reads it and never has to know how long the
/// frontmatter was. A frontmatter span has no <see cref="Offset"/> (its region is
/// the file itself, so it would only repeat <see cref="TextSpan"/>).
/// </para>
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
    /// Gets the span's start measured from the start of its own region rather than
    /// the start of the file, or <see langword="null"/> for a frontmatter span (whose
    /// region is the file itself). For a body span this counts from the start of the
    /// body — that is, <see cref="TextSpan"/> / <see cref="LineSpan"/> less the length
    /// and line count of the leading frontmatter block.
    /// </summary>
    public InstructionsFileOffset? Offset { get; init; }

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
            && Offset == other.Offset
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
        hash.Add(Offset);
        hash.Add(string.GetHashCode(Text.Span, StringComparison.Ordinal));

        return hash.ToHashCode();
    }

    /// <summary>
    /// Recovers the whole source string this span was sliced from. The span's
    /// <see cref="Text"/> is a window over the single string the parser read, so this
    /// returns that backing string without copying when possible (and the span's own
    /// text only as a fallback).
    /// </summary>
    /// <returns>The backing source string.</returns>
    internal string RecoverSourceText()
        => MemoryMarshal.TryGetString(Text, out var text, out _, out _)
            ? text
            : Text.ToString();
}
