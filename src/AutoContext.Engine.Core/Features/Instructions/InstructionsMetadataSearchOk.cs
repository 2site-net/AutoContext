namespace AutoContext.Engine.Core.Features.Instructions;

using System.Collections.Generic;

/// <summary>
/// The success arm of <see cref="InstructionsMetadataSearchResult"/>: the files
/// whose metadata satisfied the predicate, in corpus order. Disabled filtering
/// is the handler's concern; the evaluator returns every match.
/// </summary>
/// <param name="Matches">The matched files, in corpus order.</param>
internal sealed record InstructionsMetadataSearchOk(
    IReadOnlyList<InstructionsMetadataMatch> Matches)
    : InstructionsMetadataSearchResult;
