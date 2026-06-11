namespace AutoContext.Instructions.Parser.Syntax;

/// <summary>
/// A reference the <see cref="InstructionsFileSyntaxParser"/> found in the body of
/// an instructions file: what it points at (<see cref="Address"/>) together with
/// where it sits (<see cref="TextSpan"/> and <see cref="LineSpan"/>). It is
/// self-locating — it carries its own position rather than pointing back at a span.
/// </summary>
/// <param name="Address">What the reference points at, without any position.</param>
/// <param name="TextSpan">The whole-file character range the reference covers,
/// frontmatter counted in.</param>
/// <param name="LineSpan">The physical-line range the reference covers.</param>
public sealed record InstructionsFileSyntaxReference(
    InstructionsFileReferenceAddress Address,
    InstructionsFileTextSpan TextSpan,
    InstructionsFileLineSpan LineSpan);
