namespace AutoContext.Instructions.Parser;

/// <summary>
/// A syntax-driven source span emitted by <see cref="InstructionsFileSpanParser"/>.
/// A span is not a physical line: it may cover part of a line, exactly one line,
/// or several lines. Larger block spans (frontmatter blocks, rule bullets) can
/// contain smaller token spans (keys, values, tags, references), so in
/// <see cref="InstructionsFileSpanEmitLevel.Full"/> mode emitted spans may overlap.
/// </summary>
/// <param name="Text">The verbatim source text the span covers.</param>
/// <param name="Kind">The span's syntax role.</param>
/// <param name="TextSpan">The whole-file character range the span covers.</param>
/// <param name="LineSpan">The physical-line range the span covers.</param>
public sealed record InstructionsFileParsedSpan(
    string Text,
    InstructionsFileSpanKind Kind,
    InstructionsFileTextSpan TextSpan,
    InstructionsFileLineSpan LineSpan)
{
    /// <summary>A shared, empty diagnostic list — the default
    /// <see cref="Diagnostics"/> value for any span that carries no fault.</summary>
    public static IReadOnlyList<InstructionsFileSpanDiagnostic> NoDiagnostics { get; } = [];

    /// <summary>The file-local diagnostics attached to this span. Empty unless the
    /// span — or a more specific span that was filtered out by the active emit
    /// level or scope and promoted here — represents a fault.</summary>
    public IReadOnlyList<InstructionsFileSpanDiagnostic> Diagnostics { get; init; } = NoDiagnostics;
}
