namespace AutoContext.Instructions.Parser;

/// <summary>
/// A non-fatal observation the parser makes about an instruction bullet — a
/// duplicate, missing, or malformed <c>INST####</c> tag. The parser reports
/// diagnostics rather than throwing; whether any kind aborts a build is the
/// consumer's policy.
/// </summary>
/// <param name="Kind">The category of the observation.</param>
/// <param name="Line">The zero-based line index in the normalised body where the
/// observation was made.</param>
/// <param name="Message">A human-readable description of the observation.</param>
public sealed record InstructionsFileDiagnostic(
    InstructionsFileDiagnosticKind Kind,
    int Line,
    string Message);
