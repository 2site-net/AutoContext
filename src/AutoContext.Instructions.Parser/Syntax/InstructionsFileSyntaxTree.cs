namespace AutoContext.Instructions.Parser.Syntax;

/// <summary>
/// The result of one parse pass over an instructions file, split into four
/// parallel streams: the <see cref="Frontmatter"/> spans at the top of the file,
/// the <see cref="Body"/> spans below it, the <see cref="References"/> the body
/// cites, and any <see cref="Diagnostics"/>. The parser already knows which region
/// it is in as it scans, so routing each span to <see cref="Frontmatter"/> or
/// <see cref="Body"/> costs nothing extra; references and diagnostics are
/// self-locating side streams. <see cref="Model.InstructionsFile.FromSyntaxTree"/> turns
/// this into the final structured result.
/// </summary>
/// <param name="Frontmatter">The frontmatter spans, in document order: the block
/// and the key/value tokens inside it.</param>
/// <param name="Body">The body spans, in document order: headings, text, rule
/// bullets, and the tag/reference tokens inside them.</param>
/// <param name="References">The references the body cites, in document order.</param>
/// <param name="Diagnostics">The problems found, in document order.</param>
public sealed record InstructionsFileSyntaxTree(
    IReadOnlyList<InstructionsFileSyntaxSpan> Frontmatter,
    IReadOnlyList<InstructionsFileSyntaxSpan> Body,
    IReadOnlyList<InstructionsFileSyntaxReference> References,
    IReadOnlyList<InstructionsFileSyntaxDiagnostic> Diagnostics);
