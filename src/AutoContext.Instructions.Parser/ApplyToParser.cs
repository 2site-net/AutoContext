namespace AutoContext.Instructions.Parser;

/// <summary>
/// Parses an instruction file's <c>applyTo</c> frontmatter value into its
/// structural pieces. This is an internal collaborator of
/// <see cref="InstructionsFileParser"/> — the single public entry point — not a
/// second file parser: it understands only the <c>applyTo</c> glob-list
/// grammar. The parser splits comma-separated globs at brace depth zero (so
/// intra-brace commas such as <c>{cs,fs}</c> survive), brace-expands
/// <c>{a,b,c}</c> groups, and extracts the derived extension set. It
/// deliberately never canonicalises globs, simplifies <c>**</c> patterns, or
/// otherwise reasons about what a glob means; lossless-ness is asserted by
/// <see cref="FrontmatterApplyToParsedResult.RoundTrips"/>.
/// </summary>
internal static class ApplyToParser
{
    /// <summary>
    /// Parses <paramref name="applyTo"/> into its verbatim globs, the
    /// brace-expanded globs, and the derived extension set.
    /// </summary>
    /// <param name="applyTo">The raw <c>applyTo</c> frontmatter value.</param>
    /// <returns>The structural parse result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="applyTo"/> is
    /// <see langword="null"/>.</exception>
    public static FrontmatterApplyToParsedResult Parse(string applyTo)
    {
        ArgumentNullException.ThrowIfNull(applyTo);

        var globs = SplitTopLevel(applyTo, ',')
            .Select(static term => term.Trim())
            .Where(static term => term.Length > 0)
            .ToArray();

        var expanded = new List<string>();

        foreach (var glob in globs)
        {
            expanded.AddRange(ExpandBraces(glob));
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

        return new FrontmatterApplyToParsedResult(applyTo, globs, expanded, extensions);
    }

    private static List<string> ExpandBraces(string glob)
    {
        var open = glob.IndexOf('{', StringComparison.Ordinal);

        if (open < 0)
        {
            return [glob];
        }

        var close = FindMatchingBrace(glob, open);

        if (close < 0)
        {
            return [glob];
        }

        var prefix = glob[..open];
        var suffix = glob[(close + 1)..];
        var options = SplitTopLevel(glob[(open + 1)..close], ',');
        var results = new List<string>();

        foreach (var option in options)
        {
            results.AddRange(ExpandBraces(prefix + option + suffix));
        }

        return results;
    }

    private static string? ExtractExtension(string glob)
    {
        var slash = glob.LastIndexOf('/');
        var segment = slash >= 0 ? glob[(slash + 1)..] : glob;
        var dot = segment.LastIndexOf('.');

        if (dot < 0 || dot == segment.Length - 1)
        {
            return null;
        }

        var extension = segment[(dot + 1)..];

        return extension.AsSpan().IndexOfAny("*?{}") >= 0 ? null : extension;
    }

    private static int FindMatchingBrace(string glob, int open)
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

    private static List<string> SplitTopLevel(string value, char separator)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];

            if (c == '{')
            {
                depth++;
            }

            if (c == '}' && depth > 0)
            {
                depth--;
            }

            if (c == separator && depth == 0)
            {
                parts.Add(value[start..i]);
                start = i + 1;
            }
        }

        parts.Add(value[start..]);

        return parts;
    }
}
