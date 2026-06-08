namespace AutoContext.Instructions.Parser;

/// <summary>
/// The body part of an <see cref="InstructionsFileParsedContent"/>: the body text
/// that the offsets are measured against, plus everything found in a single walk
/// over it — the <c>##</c>/<c>###</c> sections, the <c>**Do**</c>/<c>**Don't**</c>
/// rule bullets, the <c>[locator#fragment]</c> references in the prose, and any
/// rule-tag or reference diagnostics. The parser fills this in one pass and pairs
/// it with the frontmatter.
/// </summary>
/// <param name="RawValue">The body text: the file with its leading frontmatter
/// block removed. All section, rule, and reference offsets are measured from the
/// start of this text.</param>
/// <param name="Sections">The <c>##</c>/<c>###</c> sections, in the order they
/// appear.</param>
/// <param name="Rules">The <c>**Do**</c>/<c>**Don't**</c> rule bullets, in the
/// order they appear.</param>
/// <param name="References">The <c>[locator#fragment]</c> rule and section
/// references found in the prose (e.g. <c>[testing#INST0014]</c> or
/// <c>[#'Assertions']</c>), in the order they appear. References inside fenced
/// code blocks and inline code are left out, since those are examples rather than
/// real links. See <see cref="InstructionsFileReference"/> for the exact form and
/// what <em>locator</em> and <em>fragment</em> mean.</param>
/// <param name="Diagnostics">Problems found with rule tags (malformed, missing, or
/// duplicate) and with references, in the order they appear.</param>
public sealed record InstructionsFileParsedBody(
    string RawValue,
    IReadOnlyList<InstructionsFileSection> Sections,
    IReadOnlyList<InstructionsFileRule> Rules,
    IReadOnlyList<InstructionsFileReference> References,
    IReadOnlyList<InstructionsFileDiagnostic> Diagnostics);
