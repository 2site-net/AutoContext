namespace AutoContext.Instructions.Parser;

/// <summary>
/// One file-local diagnostic discovered while parsing an instructions file. The
/// span parser attaches it to the <see cref="InstructionsFileParsedSpan"/> that
/// represents the fault; the structured parser later resolves <see cref="Line"/>
/// to the body-relative coordinate consumers read. The <see cref="Kind"/> names
/// the syntax problem; the <see cref="Message"/> describes it for a human reader.
/// </summary>
/// <param name="Kind">The category of the fault.</param>
/// <param name="Message">A human-readable description of the fault.</param>
/// <param name="Line">The zero-based line index in the frontmatter-stripped body
/// where the fault was found, or <c>-1</c> before the structured parser resolves
/// it from the owning span's whole-file coordinates.</param>
public sealed record InstructionsFileDiagnostic(
    InstructionsFileDiagnosticKind Kind,
    string Message,
    int Line = -1);
