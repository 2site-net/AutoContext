namespace AutoContext.Engine.Core.Features.Instructions;

/// <summary>
/// One ranked file hit of the instruction-body full-text search: a corpus
/// file whose projected body and/or description matched every distinct
/// query token, with the matched excerpts. The identity fields
/// (<see cref="Key"/>, <see cref="FileName"/>, <see cref="Name"/>) and
/// <see cref="Description"/> mirror the corpus row so the RPC handler can
/// map straight onto the wire DTO.
/// </summary>
/// <param name="Key">The file basename (the stable key).</param>
/// <param name="FileName">The corpus file name including the
/// <c>.instructions.md</c> extension.</param>
/// <param name="Name">The raw frontmatter name (<c>&lt;key&gt; (vX.Y.Z)</c>).</param>
/// <param name="Description">The trimmed frontmatter description.</param>
/// <param name="Score">The relevance score; higher ranks first.</param>
/// <param name="Excerpts">The matched body excerpts, ordered by earliest
/// position in the body.</param>
internal sealed record InstructionsContentHit(
    string Key,
    string FileName,
    string Name,
    string Description,
    double Score,
    IReadOnlyList<InstructionsContentExcerpt> Excerpts);
