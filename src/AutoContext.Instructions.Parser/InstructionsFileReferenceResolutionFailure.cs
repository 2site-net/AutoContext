namespace AutoContext.Instructions.Parser;

/// <summary>
/// One cross-file resolution fault: a parsed <see cref="InstructionsFileReference"/>
/// that does not point at a real rule or section once the whole corpus is known.
/// The <see cref="Reference"/> carries the original token and its body offsets, so
/// a consumer can surface the finding at the exact source location.
/// </summary>
/// <param name="Kind">Why the reference failed to resolve.</param>
/// <param name="Reference">The offending reference, verbatim from the parse.</param>
/// <param name="Message">A human-readable description of the fault.</param>
public sealed record InstructionsFileReferenceResolutionFailure(
    InstructionsFileReferenceFindingKind Kind,
    InstructionsFileReference Reference,
    string Message);
