namespace AutoContext.Instructions.Parser;

/// <summary>
/// A syntax-driven source span emitted by <see cref="InstructionsFileSpanParser"/>.
/// A span is not a physical line: it may cover part of a line, exactly one line,
/// or several lines. Larger block spans (frontmatter blocks, rule bullets) can
/// contain smaller token spans (keys, values, tags, references), so in
/// <see cref="InstructionsFileSpanEmitLevel.Full"/> mode emitted spans may overlap.
/// </summary>
/// <param name="Text">The verbatim source text the span covers, as a zero-copy
/// slice of the backing file buffer (equivalent to the substring at
/// <see cref="TextSpan"/>).</param>
/// <param name="Kind">The span's syntax role.</param>
/// <param name="TextSpan">The whole-file character range the span covers.</param>
/// <param name="LineSpan">The physical-line range the span covers.</param>
public sealed record InstructionsFileParsedSpan(
    ReadOnlyMemory<char> Text,
    InstructionsFileSpanKind Kind,
    InstructionsFileTextSpan TextSpan,
    InstructionsFileLineSpan LineSpan)
{
    /// <summary>A shared, empty diagnostic list — the default
    /// <see cref="Diagnostics"/> value for any span that carries no fault.</summary>
    public static IReadOnlyList<InstructionsFileDiagnostic> NoDiagnostics { get; } = [];

    /// <summary>The file-local diagnostics attached to this span. Empty unless the
    /// span — or a more specific span that was filtered out by the active emit
    /// level or scope and promoted here — represents a fault.</summary>
    public IReadOnlyList<InstructionsFileDiagnostic> Diagnostics { get; init; } = NoDiagnostics;

    /// <summary>The coordinate-free classification of a
    /// <see cref="InstructionsFileSpanKind.Reference"/> span, or
    /// <see langword="null"/> for any other kind or for a malformed reference whose
    /// fault is carried in <see cref="Diagnostics"/> instead.</summary>
    public InstructionsFileReferenceAddress? ReferenceAddress { get; init; }

    /// <summary>
    /// Determines whether this span equals <paramref name="other"/> by value. The
    /// synthesised record equality is overridden because <see cref="Text"/> is a
    /// <see cref="ReadOnlyMemory{T}"/>, whose default equality compares the backing
    /// reference and slice bounds rather than the characters; this override compares
    /// <see cref="Text"/> by content and <see cref="Diagnostics"/> element-wise, so
    /// two spans with identical content compare equal even when sliced from
    /// different buffers.
    /// </summary>
    /// <param name="other">The span to compare against.</param>
    /// <returns><see langword="true"/> if the spans are equal by value.</returns>
    public bool Equals(InstructionsFileParsedSpan? other)
        => other is not null
            && Kind == other.Kind
            && TextSpan == other.TextSpan
            && LineSpan == other.LineSpan
            && Text.Span.SequenceEqual(other.Text.Span)
            && ReferenceAddress == other.ReferenceAddress
            && Diagnostics.SequenceEqual(other.Diagnostics);

    /// <summary>
    /// Computes a hash code consistent with <see cref="Equals(InstructionsFileParsedSpan)"/>,
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
        hash.Add(ReferenceAddress);

        foreach (var diagnostic in Diagnostics)
        {
            hash.Add(diagnostic);
        }

        return hash.ToHashCode();
    }
}
