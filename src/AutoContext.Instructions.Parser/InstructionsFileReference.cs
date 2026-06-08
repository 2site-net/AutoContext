namespace AutoContext.Instructions.Parser;

/// <summary>
/// One bare cross-reference token of the form <c>[locator#fragment]</c> found in
/// an instructions file's prose — the machine-readable way one rule cites another
/// rule or a whole section. The mandatory <c>#</c> separates an optional
/// <em>locator</em> (the target file) from the <em>fragment</em> (the target rule
/// id or quoted heading); an absent locator means the reference is same-file.
/// Definitions (<c>[INST####]</c> bullet tags, which carry no <c>#</c>) are not
/// references and never appear here. The parser records references verbatim and
/// only checks their syntax; resolving a locator to a real file, or a target to a
/// real rule or section, is a later cross-file concern.
/// </summary>
/// <param name="Address">The coordinate-free classification of the reference: the
/// target kind together with the normalised locator and target.</param>
/// <param name="TextSpan">The character range of the whole token — from the opening
/// <c>[</c> to just past the closing <c>]</c> — in normalised
/// (frontmatter-stripped) body coordinates.</param>
/// <param name="Line">The zero-based line index of the reference within the
/// normalised (frontmatter-stripped) body.</param>
public sealed record InstructionsFileReference(
    InstructionsFileReferenceAddress Address,
    InstructionsFileTextSpan TextSpan,
    int Line);
