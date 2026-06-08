namespace AutoContext.Instructions.Parser;

/// <summary>
/// The coordinate-free classification of a well-formed
/// <see cref="InstructionsFileSpanKind.Reference"/> token: the discriminated
/// target kind together with the normalised locator and target. The span parser
/// computes this once for every well-formed reference and attaches it to the
/// reference span; the structured parser reads it back and supplies the
/// body-relative position that only it can resolve. A reference span whose
/// classification is <see langword="null"/> is malformed — its fault is carried in
/// the span's diagnostics instead.
/// </summary>
/// <param name="Kind">Whether the fragment targets a rule or a section.</param>
/// <param name="Locator">The target file locator — a catalog key, a filename, or a
/// URI — or <see langword="null"/> when the reference omits the locator and is
/// therefore same-file.</param>
/// <param name="Target">The cited target: the verbatim <c>INST####</c> id for a
/// <see cref="InstructionsFileReferenceKind.Rule"/> reference, or the heading text
/// for a <see cref="InstructionsFileReferenceKind.Section"/> reference with the
/// surrounding quotes removed and any backslash escapes resolved.</param>
public readonly record struct InstructionsFileReferenceAddress(
    InstructionsFileReferenceKind Kind,
    string? Locator,
    string Target);
