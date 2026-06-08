namespace AutoContext.Instructions.Parser;

/// <summary>
/// One file-local diagnostic discovered while parsing an instructions file,
/// attached to the <see cref="InstructionsFileParsedSpan"/> that represents the
/// fault. The <see cref="Kind"/> names the syntax problem; the
/// <see cref="Message"/> describes it for a human reader.
/// </summary>
/// <param name="Kind">The category of the fault.</param>
/// <param name="Message">A human-readable description of the fault.</param>
public sealed record InstructionsFileSpanDiagnostic(
    InstructionsFileSpanDiagnosticKind Kind,
    string Message);
