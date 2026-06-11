namespace AutoContext.Instructions.Parser.Syntax;

/// <summary>
/// A problem the <see cref="InstructionsFileSyntaxParser"/> found, paired with
/// where it sits in the file. The <see cref="Diagnostic"/> carries the kind and
/// message; <see cref="TextSpan"/> and <see cref="LineSpan"/> say which characters
/// and lines it covers, measured from the start of the file with the frontmatter
/// counted in. It is self-locating — it carries its own position rather than
/// pointing back at a span.
/// </summary>
/// <param name="Diagnostic">The problem found, with its kind and message.</param>
/// <param name="TextSpan">The whole-file character range the problem covers,
/// frontmatter counted in.</param>
/// <param name="LineSpan">The physical-line range the problem covers.</param>
public sealed record InstructionsFileSyntaxDiagnostic(
    InstructionsFileDiagnostic Diagnostic,
    InstructionsFileTextSpan TextSpan,
    InstructionsFileLineSpan LineSpan);
