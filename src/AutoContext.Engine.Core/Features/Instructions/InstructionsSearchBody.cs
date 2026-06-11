namespace AutoContext.Engine.Core.Features.Instructions;

using AutoContext.Instructions.Parser.Model;

/// <summary>
/// The whole projected body of one instructions file, as
/// <see cref="InstructionsBodyProjector.ToSearchBodyAsync"/> hands it to the
/// full-text search index: the filtered body text (frontmatter stripped,
/// disabled-rule lines removed) and the section index re-derived against
/// that text so every <see cref="InstructionsFileSection.TextSpan"/> offset
/// is measured from the start of <see cref="Content"/>.
/// </summary>
/// <param name="Content">The projected body text the search index tokenizes
/// and slices excerpts from.</param>
/// <param name="Sections">The <c>##</c>/<c>###</c> section index, in document
/// order, with offsets aligned to <see cref="Content"/>.</param>
internal sealed record InstructionsSearchBody(
    string Content,
    IReadOnlyList<InstructionsFileSection> Sections);
