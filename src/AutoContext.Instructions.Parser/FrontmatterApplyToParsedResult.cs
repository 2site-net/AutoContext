namespace AutoContext.Instructions.Parser;

/// <summary>
/// The structural parse of an instructions file's <c>applyTo</c> frontmatter
/// value, surfaced as part of the unified <see cref="InstructionsFileParsedResult"/>.
/// <paramref name="RawValue"/> is the original string the parse derives from.
/// <paramref name="Globs"/> is the verbatim, brace-depth-aware comma split of
/// that string — the canonical form that <see cref="RoundTrips"/> recomposes.
/// <paramref name="ExpandedGlobs"/> brace-expands each glob
/// (<c>**/*.{cs,fs,vb}</c> becomes three globs) and <paramref name="Extensions"/>
/// is the derived, dotless, case-insensitive extension index the coarse
/// workspace filter intersects against. The derived views never mutate the
/// verbatim globs: this is structural parsing, not glob algebra.
/// </summary>
/// <param name="RawValue">The original <c>applyTo</c> frontmatter value the parse
/// derives from.</param>
/// <param name="Globs">The verbatim glob terms, comma-split at brace depth
/// zero and trimmed, with empty terms dropped.</param>
/// <param name="ExpandedGlobs">Each glob with its <c>{a,b,c}</c> groups
/// expanded into individual globs, in declaration order.</param>
/// <param name="Extensions">The dotless file extensions named by the expanded
/// globs, compared case-insensitively; empty when no glob names a concrete
/// extension.</param>
public sealed record FrontmatterApplyToParsedResult(
    string RawValue,
    IReadOnlyList<string> Globs,
    IReadOnlyList<string> ExpandedGlobs,
    IReadOnlySet<string> Extensions)
{
    /// <summary>
    /// Gets a value indicating whether recomposing <see cref="Globs"/> reproduces
    /// <see cref="RawValue"/> modulo whitespace — the invariant that proves the
    /// structural parse loses nothing. The build-time generator enforces this
    /// per corpus file.
    /// </summary>
    public bool RoundTrips
    {
        get
        {
            var rawValue = RawValue.AsSpan();
            var position = 0;

            for (var index = 0; index < Globs.Count; index++)
            {
                if (index > 0 && !TryMatchNonWhitespace(rawValue, ref position, ','))
                {
                    return false;
                }

                foreach (var character in Globs[index].AsSpan())
                {
                    if (!char.IsWhiteSpace(character)
                        && !TryMatchNonWhitespace(rawValue, ref position, character))
                    {
                        return false;
                    }
                }
            }

            while (position < rawValue.Length)
            {
                if (!char.IsWhiteSpace(rawValue[position]))
                {
                    return false;
                }

                position++;
            }

            return true;
        }
    }

    private static bool TryMatchNonWhitespace(ReadOnlySpan<char> rawValue, ref int position, char expected)
    {
        while (position < rawValue.Length && char.IsWhiteSpace(rawValue[position]))
        {
            position++;
        }

        if (position >= rawValue.Length || rawValue[position] != expected)
        {
            return false;
        }

        position++;
        return true;
    }
}
