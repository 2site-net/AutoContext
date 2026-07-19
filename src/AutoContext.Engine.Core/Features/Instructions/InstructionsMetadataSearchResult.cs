namespace AutoContext.Engine.Core.Features.Instructions;

/// <summary>
/// Discriminated result of
/// <see cref="InstructionsMetadataSearchService.Evaluate"/>: either
/// <see cref="InstructionsMetadataSearchOk"/> with the matched files or
/// <see cref="InstructionsMetadataSearchError"/> with a structured predicate
/// fault. Faults are returned, never thrown — the metadata surface must always
/// reply with a structured envelope.
/// </summary>
internal abstract record InstructionsMetadataSearchResult;
