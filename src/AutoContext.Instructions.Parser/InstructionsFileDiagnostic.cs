namespace AutoContext.Instructions.Parser;

/// <summary>
/// One problem found while parsing a single instructions file. The span parser
/// attaches it to the <see cref="InstructionsFileParsedSpan"/> where the problem
/// is; the structured parser later sets <see cref="Line"/> to the body-relative
/// line number that consumers read. <see cref="Kind"/> says what the problem is;
/// <see cref="Message"/> spells it out for a person.
/// </summary>
/// <param name="Kind">The category of the fault.</param>
/// <param name="Message">A human-readable description of the fault.</param>
/// <param name="Line">The zero-based line number in the body (with the frontmatter
/// removed) where the problem is, or <c>-1</c> until the structured parser fills it
/// in from the owning span's whole-file position.</param>
public sealed record InstructionsFileDiagnostic(
    InstructionsFileDiagnosticKind Kind,
    string Message,
    int Line = -1);
