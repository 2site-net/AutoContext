namespace AutoContext.Engine.Core.Features.Instructions;

/// <summary>
/// The per-request projection of one instruction file's body produced by
/// <see cref="InstructionsFileService"/>: the frontmatter-stripped body
/// with disabled rules filtered out and (when requested) sliced to a set
/// of sections, paired with the anchors that were and were not resolved.
/// The <c>[INSTxxxx]</c> tags on the surviving rules are preserved so
/// cross-rule and cross-file references stay navigable.
/// </summary>
/// <param name="Content">The projected body text. Empty when every
/// requested section was unresolved or every line was filtered out.</param>
/// <param name="ReturnedSections">The section anchors actually included in
/// <see cref="Content"/>, in document order. When the request sliced, the
/// resolved subset of the requested anchors; when it did not, the file's
/// full section set.</param>
/// <param name="NotFoundSections">The requested section anchors that did
/// not resolve to a section, in request order. Empty when the request did
/// not slice or every requested anchor resolved.</param>
internal sealed record InstructionsBodyProjection(
    string Content,
    IReadOnlyList<string> ReturnedSections,
    IReadOnlyList<string> NotFoundSections);
