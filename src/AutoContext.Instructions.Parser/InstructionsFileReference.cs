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
/// <param name="Kind">Whether the fragment targets a rule or a section.</param>
/// <param name="Locator">The target file locator — a catalogue key
/// (<c>testing</c>), a filename, or a URI — or <see langword="null"/> when the
/// reference omits the locator and is therefore same-file.</param>
/// <param name="Target">The cited target: the verbatim <c>INST####</c> id for a
/// <see cref="InstructionsFileReferenceKind.Rule"/> reference, or the heading text
/// for a <see cref="InstructionsFileReferenceKind.Section"/> reference with the
/// surrounding quotes removed and any backslash escapes resolved (so a heading
/// containing an apostrophe, written <c>[#'Bob\'s rules']</c>, has the target
/// <c>Bob's rules</c>).</param>
/// <param name="Line">The zero-based line index of the reference within the
/// normalised body.</param>
/// <param name="CharStart">The offset of the opening <c>[</c> into the normalised
/// (frontmatter-stripped) body.</param>
/// <param name="CharEnd">The exclusive offset just past the closing <c>]</c>.</param>
public sealed record InstructionsFileReference(
    InstructionsFileReferenceKind Kind,
    string? Locator,
    string Target,
    int Line,
    int CharStart,
    int CharEnd);
