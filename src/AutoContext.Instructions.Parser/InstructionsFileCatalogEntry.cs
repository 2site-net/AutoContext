namespace AutoContext.Instructions.Parser;

/// <summary>
/// One file's contribution to an <see cref="InstructionsFileCatalog"/>: the
/// catalog key that locators target, the set of <c>INST####</c> rule ids the
/// file defines, and the file's section index. This is the projection a
/// cross-file reference resolves against — a rule reference checks
/// <see cref="RuleIds"/> for membership, a section reference matches
/// <see cref="Sections"/> by anchor or heading.
/// </summary>
/// <param name="Key">The catalog key — the file name stem with the
/// <c>.instructions.md</c> suffix removed (e.g. <c>testing</c>) — that a
/// <see cref="InstructionsFileReference.Locator"/> resolves to.</param>
/// <param name="RuleIds">The <c>INST####</c> ids of every tagged rule the file
/// defines. Untagged bullets contribute nothing because they cannot be
/// referenced.</param>
/// <param name="Sections">The file's <c>##</c>/<c>###</c> section index, used to
/// resolve section references by anchor or heading.</param>
public sealed record InstructionsFileCatalogEntry(
    string Key,
    IReadOnlySet<string> RuleIds,
    IReadOnlyList<InstructionsFileSection> Sections);
