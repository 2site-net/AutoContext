namespace AutoContext.Instructions.Parser;

/// <summary>
/// One <c>[locator#fragment]</c> reference found in an instructions file's prose —
/// the way one rule points at another rule or a whole section. The required
/// <c>#</c> splits an optional <em>locator</em> (which file) from the
/// <em>fragment</em> (which rule id or quoted heading); leaving the locator out
/// means the reference points within the same file. A bullet tag
/// (<c>[INST####]</c>, with no <c>#</c>) defines a rule rather than pointing at one,
/// so it never shows up here. The parser records each reference as written and only
/// checks its form; matching a locator to a real file, or a target to a real rule
/// or section, happens later once all files are known.
/// </summary>
/// <param name="Address">What the reference points at, without any position: the
/// target kind together with its locator and target.</param>
/// <param name="TextSpan">The character range of the whole <c>[…]</c> token, from
/// the opening <c>[</c> to just past the closing <c>]</c>, measured against the
/// body (with the frontmatter removed).</param>
/// <param name="Line">The zero-based line number of the reference within the body
/// (with the frontmatter removed).</param>
public sealed record InstructionsFileReference(
    InstructionsFileReferenceAddress Address,
    InstructionsFileTextSpan TextSpan,
    int Line);
