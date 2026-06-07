namespace AutoContext.Instructions.Parser;

/// <summary>
/// The body half of an <see cref="InstructionsFileParsedContent"/>: the normalised
/// body the offsets are relative to plus everything a single walk over it yields —
/// the <c>##</c>/<c>###</c> section index, the actionable <c>**Do**</c>/<c>**Don't**</c>
/// rule bullets, the bare <c>[locator#fragment]</c> cross-reference tokens, and any
/// bullet-tag or reference diagnostics. <see cref="InstructionsFileParser.Parse"/>
/// fills this in one pass and pairs it with the parsed frontmatter.
/// </summary>
/// <param name="RawValue">The normalised body: the file content with its leading
/// frontmatter block stripped. All section, rule, and reference offsets are
/// relative to it.</param>
/// <param name="Sections">The <c>##</c>/<c>###</c> section index, in document
/// order.</param>
/// <param name="Rules">The actionable <c>**Do**</c>/<c>**Don't**</c> bullets, in
/// document order.</param>
/// <param name="References">The bare <c>[locator#fragment]</c> rule and section
/// references found in prose (e.g. <c>[testing#INST0014]</c> or
/// <c>[#'Assertions']</c>), in document order. Excludes references inside fenced
/// code blocks and inline code spans (those are syntax examples, not live
/// references). See <see cref="InstructionsFileReference"/> for the token grammar
/// and what <em>locator</em> and <em>fragment</em> mean.</param>
/// <param name="Diagnostics">Observations about malformed, missing, or duplicate
/// rule tags and malformed references, in document order.</param>
public sealed record InstructionsFileParsedBody(
    string RawValue,
    IReadOnlyList<InstructionsFileSection> Sections,
    IReadOnlyList<InstructionsFileRule> Rules,
    IReadOnlyList<InstructionsFileReference> References,
    IReadOnlyList<InstructionsFileDiagnostic> Diagnostics);
