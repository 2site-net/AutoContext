namespace AutoContext.Instructions.Parser.Syntax;

/// <summary>
/// What a well-formed <see cref="InstructionsFileSpanKind.Reference"/> token points
/// at, with no position attached: the target kind together with its locator and
/// target. The <see cref="InstructionsFileSyntaxParser"/> works this out once for
/// each well-formed reference and attaches it to the reference span;
/// <see cref="Model.InstructionsFile.FromSpans"/> reads it back and adds the
/// body-relative position, which only it can work out. When this is
/// <see langword="null"/> the reference is malformed, and the problem is reported
/// in the span's diagnostics instead.
/// </summary>
/// <param name="Kind">Whether the fragment points at a rule or a section.</param>
/// <param name="Locator">Which file is being pointed at — a catalog key, a
/// filename, or a URI — or <see langword="null"/> when the reference leaves the
/// locator out and so points within the same file.</param>
/// <param name="Target">What is being pointed at: the <c>INST####</c> id as written
/// for a <see cref="InstructionsFileReferenceKind.Rule"/> reference, or the heading
/// text for a <see cref="InstructionsFileReferenceKind.Section"/> reference, with
/// the surrounding quotes removed and any backslash escapes undone.</param>
public readonly record struct InstructionsFileReferenceAddress(
    InstructionsFileReferenceKind Kind,
    string? Locator,
    string Target);
