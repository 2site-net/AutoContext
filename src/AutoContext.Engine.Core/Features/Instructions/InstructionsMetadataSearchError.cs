namespace AutoContext.Engine.Core.Features.Instructions;

/// <summary>
/// The fault arm of <see cref="InstructionsMetadataSearchResult"/>: a
/// structured predicate fault the handler projects onto the wire error
/// envelope, alongside the frozen
/// <see cref="InstructionsMetadataSearchService.RecognizedFields"/> schema.
/// </summary>
/// <param name="Kind">The fault code.</param>
/// <param name="Field">The offending predicate field name.</param>
/// <param name="Reason">A human-readable explanation of the fault.</param>
internal sealed record InstructionsMetadataSearchError(
    InstructionsMetadataSearchErrorKind Kind,
    string Field,
    string Reason)
    : InstructionsMetadataSearchResult;
