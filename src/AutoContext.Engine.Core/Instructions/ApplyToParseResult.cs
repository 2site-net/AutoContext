namespace AutoContext.Engine.Core.Instructions;

/// <summary>
/// The structural parse of an instruction file's <c>applyTo</c> frontmatter
/// value. <paramref name="Globs"/> is the verbatim, brace-depth-aware comma
/// split of the original string — the canonical form that round-trips (see
/// <see cref="ApplyToParser.RoundTrips"/>). <paramref name="ExpandedGlobs"/>
/// brace-expands each glob (<c>**/*.{cs,fs,vb}</c> becomes three globs) and
/// <paramref name="Extensions"/> is the derived, dotless, case-insensitive
/// extension index the coarse workspace filter intersects against. The two
/// derived views never mutate the verbatim globs: this is structural parsing,
/// not glob algebra.
/// </summary>
/// <param name="Globs">The verbatim glob terms, comma-split at brace depth
/// zero and trimmed, with empty terms dropped.</param>
/// <param name="ExpandedGlobs">Each glob with its <c>{a,b,c}</c> groups
/// expanded into individual globs, in declaration order.</param>
/// <param name="Extensions">The dotless file extensions named by the expanded
/// globs, compared case-insensitively; empty when no glob names a concrete
/// extension.</param>
internal sealed record ApplyToParseResult(
    IReadOnlyList<string> Globs,
    IReadOnlyList<string> ExpandedGlobs,
    IReadOnlySet<string> Extensions);
