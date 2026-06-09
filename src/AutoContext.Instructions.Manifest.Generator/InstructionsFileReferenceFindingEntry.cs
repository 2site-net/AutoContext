namespace AutoContext.Instructions.Manifest.Generator;

using AutoContext.Instructions.Parser.Model;

/// <summary>
/// One cross-file reference fault tied back to the corpus file it was found in.
/// The underlying <see cref="InstructionsFileReferenceResolutionFailure"/> records why a
/// reference failed to resolve and where in the body it sits; this wrapper adds
/// the owning file's identity so a build report can point at the exact source.
/// </summary>
/// <param name="SourceKey">The catalog key of the file the reference was parsed from.</param>
/// <param name="SourceFileName">The file name of the file the reference was parsed from.</param>
/// <param name="Failure">The cross-file resolution fault.</param>
internal sealed record InstructionsFileReferenceFindingEntry(
    string SourceKey,
    string SourceFileName,
    InstructionsFileReferenceResolutionFailure Failure);
