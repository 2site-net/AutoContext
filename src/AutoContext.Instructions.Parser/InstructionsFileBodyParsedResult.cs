namespace AutoContext.Instructions.Parser;

/// <summary>
/// The body half of an <see cref="InstructionsFileParsedResult"/>: the normalised
/// body the offsets are relative to plus everything a single walk over it yields —
/// the <c>##</c>/<c>###</c> section index, the actionable <c>**Do**</c>/<c>**Don't**</c>
/// rule bullets, and any bullet-tag diagnostics. <see cref="InstructionsFileParser.Parse"/>
/// fills this in one pass and pairs it with the parsed frontmatter.
/// </summary>
/// <param name="RawBody">The normalised body: the file content with its leading
/// frontmatter block stripped. All section and rule offsets are relative to it.</param>
/// <param name="Sections">The <c>##</c>/<c>###</c> section index, in document
/// order.</param>
/// <param name="Rules">The actionable <c>**Do**</c>/<c>**Don't**</c> bullets, in
/// document order.</param>
/// <param name="Diagnostics">Observations about malformed, missing, or duplicate
/// rule tags, in document order.</param>
public sealed record InstructionsFileBodyParsedResult(
    string RawBody,
    IReadOnlyList<InstructionsFileSection> Sections,
    IReadOnlyList<InstructionsFileRule> Rules,
    IReadOnlyList<InstructionsFileDiagnostic> Diagnostics);
