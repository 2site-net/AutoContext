namespace AutoContext.Instructions.Parser;

using AutoContext.Instructions.Parser.Model;

/// <summary>
/// Parses an instructions file's <c>applyTo</c> frontmatter value into its
/// structural pieces. This is an internal collaborator of
/// <see cref="InstructionsFile"/> — the structural file parser — not a
/// second file parser: it understands only the <c>applyTo</c> glob-list
/// grammar. The parser splits comma-separated globs at brace depth zero (so
/// intra-brace commas such as <c>{cs,fs}</c> survive), brace-expands
/// <c>{a,b,c}</c> groups, and extracts the derived extension set. It
/// deliberately never canonicalises globs, simplifies <c>**</c> patterns, or
/// otherwise reasons about what a glob means; lossless-ness is asserted by
/// <see cref="FrontmatterApplyTo.RoundTrips"/>.
/// </summary>
internal static class FrontmatterApplyToParser
{
    /// <summary>
    /// Parses <paramref name="applyTo"/> into its verbatim globs, the
    /// brace-expanded globs, and the derived extension set.
    /// </summary>
    /// <param name="applyTo">The raw <c>applyTo</c> frontmatter value.</param>
    /// <returns>The structural parse result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="applyTo"/> is
    /// <see langword="null"/>.</exception>
    public static FrontmatterApplyTo Parse(string applyTo)
    {
        ArgumentNullException.ThrowIfNull(applyTo);

        var globs = SplitGlobs(applyTo);

        var expanded = new List<string>();

        foreach (var glob in globs)
        {
            ExpandBraces(glob, expanded);
        }

        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var glob in expanded)
        {
            var extension = ExtractExtension(glob);

            if (extension is not null)
            {
                extensions.Add(extension);
            }
        }

        return new FrontmatterApplyTo(applyTo, globs, expanded, extensions);
    }

    private static void ExpandBraces(ReadOnlySpan<char> glob, List<string> results)
    {
        var open = glob.IndexOf('{');

        if (open < 0)
        {
            results.Add(glob.ToString());
            return;
        }

        var close = FindMatchingBrace(glob, open);

        if (close < 0)
        {
            results.Add(glob.ToString());
            return;
        }

        var prefix = glob[..open];
        var suffix = glob[(close + 1)..];

        foreach (var option in new TopLevelSplitEnumerator(glob[(open + 1)..close]))
        {
            ExpandBraces(string.Concat(prefix, option, suffix), results);
        }
    }

    private static string? ExtractExtension(ReadOnlySpan<char> glob)
    {
        var slash = glob.LastIndexOf('/');
        var segment = slash >= 0 ? glob[(slash + 1)..] : glob;
        var dot = segment.LastIndexOf('.');

        if (dot < 0 || dot == segment.Length - 1)
        {
            return null;
        }

        var extension = segment[(dot + 1)..];

        return extension.IndexOfAny("*?{}") >= 0 ? null : extension.ToString();
    }

    private static int FindMatchingBrace(ReadOnlySpan<char> glob, int open)
    {
        var depth = 0;

        for (var i = open; i < glob.Length; i++)
        {
            if (glob[i] == '{')
            {
                depth++;
            }

            if (glob[i] == '}')
            {
                depth--;

                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static string[] SplitGlobs(ReadOnlySpan<char> applyTo)
    {
        var globs = new List<string>();

        foreach (var term in new TopLevelSplitEnumerator(applyTo))
        {
            var trimmed = term.Trim();

            if (!trimmed.IsEmpty)
            {
                globs.Add(trimmed.ToString());
            }
        }

        return [.. globs];
    }

    /// <summary>
    /// Splits a span on top-level commas — those at brace depth zero — yielding
    /// each segment as a <see cref="ReadOnlySpan{T}"/> without allocating. Commas
    /// inside <c>{...}</c> groups stay within their segment, and empty segments
    /// are yielded verbatim so brace options such as <c>{a,,b}</c> survive.
    /// </summary>
    private ref struct TopLevelSplitEnumerator(ReadOnlySpan<char> value)
    {
        private bool _exhausted = false;
        private int _start = 0;
        private readonly ReadOnlySpan<char> _value = value;

        /// <summary>Gets the segment yielded by the most recent
        /// <see cref="MoveNext"/>.</summary>
        public ReadOnlySpan<char> Current { get; private set; } = default;

        /// <summary>Returns this enumerator so it can drive a <c>foreach</c>.</summary>
        /// <returns>This enumerator.</returns>
        public readonly TopLevelSplitEnumerator GetEnumerator()
            => this;

        /// <summary>Advances to the next top-level segment.</summary>
        /// <returns><see langword="true"/> if a segment was produced;
        /// <see langword="false"/> once the span is exhausted.</returns>
        public bool MoveNext()
        {
            if (_exhausted)
            {
                return false;
            }

            var depth = 0;

            for (var i = _start; i < _value.Length; i++)
            {
                var c = _value[i];

                if (c == '{')
                {
                    depth++;
                }

                if (c == '}' && depth > 0)
                {
                    depth--;
                }

                if (c == ',' && depth == 0)
                {
                    Current = _value[_start..i];
                    _start = i + 1;
                    return true;
                }
            }

            Current = _value[_start..];
            _exhausted = true;
            return true;
        }
    }
}
