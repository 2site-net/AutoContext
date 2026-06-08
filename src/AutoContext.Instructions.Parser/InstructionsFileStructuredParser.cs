namespace AutoContext.Instructions.Parser;

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Turns the flat list of spans from <see cref="InstructionsFileSpanParser"/> into
/// a structured <see cref="InstructionsFileParsedContent"/> — the frontmatter, the
/// list of <c>##</c>/<c>###</c> sections, the rule bullets, the
/// <c>[locator#fragment]</c> references, and any diagnostics. The span parser does
/// the raw scanning; this second pass gives the result its shape, so callers get
/// one ready-to-use object instead of a stream of loose pieces.
/// <para>
/// One thing it sorts out is positions. The spans count from the start of the
/// file, frontmatter included, but callers expect positions measured from the
/// start of the body, as if the frontmatter were not there. The leading
/// <see cref="InstructionsFileSpanKind.FrontmatterBlock"/> span says how long the
/// frontmatter is, and this pass subtracts that from every offset before handing
/// the result back.
/// </para>
/// </summary>
internal sealed partial class InstructionsFileStructuredParser
{
    private InstructionsFileSpanParser? _spanParser;

    private enum FrontmatterField
    {
        Unknown,
        Name,
        Description,
        ApplyTo,
    }

    /// <summary>
    /// Builds the structured parse from a span stream. The spans must be the
    /// complete <see cref="InstructionsFileSpanEmitLevel.Full"/> /
    /// <see cref="InstructionsFileSpanEmitScope.All"/> output for a single file, in
    /// document order: the block spans supply the verbatim text, and the token spans
    /// nested inside them supply the frontmatter fields, tags, and references. The
    /// stream is read once, front to back, without being buffered into a list.
    /// </summary>
    /// <param name="spans">The span stream.</param>
    /// <returns>The complete structural parse.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="spans"/> is
    /// <see langword="null"/>.</exception>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Kept an instance method so the parser presents one coherent instance API surface alongside the stateful ParseFileAsync.")]
    public InstructionsFileParsedContent Parse(
        IEnumerable<InstructionsFileParsedSpan> spans)
    {
        ArgumentNullException.ThrowIfNull(spans);

        var rawContent = new StringBuilder();
        var frontmatterCharLength = 0;
        var frontmatterLineCount = 0;
        var frontmatterRawValue = string.Empty;
        string? name = null;
        string? description = null;
        string? applyToRaw = null;
        List<RawHeading>? rawHeadings = null;
        List<InstructionsFileRule>? rules = null;
        List<InstructionsFileReference>? references = null;
        List<InstructionsFileDiagnostic>? diagnostics = null;
        string? lastSectionHeading = null;
        var currentFrontmatterField = FrontmatterField.Unknown;

        foreach (var span in spans)
        {
            var kind = span.Kind;

            if (IsBlockKind(kind))
            {
                rawContent.Append(span.Text.Span);
            }

            if (kind == InstructionsFileSpanKind.FrontmatterBlock)
            {
                frontmatterCharLength = span.TextSpan.Length;
                frontmatterLineCount = span.LineSpan.LineCount;

                var block = GeneratedFrontmatterBlockRegex().Match(span.Text.ToString());
                frontmatterRawValue = block.Success ? block.Groups[1].Value : string.Empty;
            }
            else if (kind == InstructionsFileSpanKind.FrontmatterKey)
            {
                currentFrontmatterField = ClassifyFrontmatterKey(span.Text.Span);
                AssignFrontmatterField(currentFrontmatterField, string.Empty);
            }
            else if (kind == InstructionsFileSpanKind.FrontmatterValue)
            {
                AssignFrontmatterField(currentFrontmatterField, span.Text.ToString());
            }
            else if (kind is InstructionsFileSpanKind.Heading2 or InstructionsFileSpanKind.Heading3)
            {
                var level = kind == InstructionsFileSpanKind.Heading2 ? 2 : 3;
                var text = ParseHeadingText(span.Text.Span);
                var parent = level == 2 ? null : lastSectionHeading;

                if (level == 2)
                {
                    lastSectionHeading = text;
                }

                (rawHeadings ??= []).Add(new RawHeading(level, text, parent, span.TextSpan.StartIndex - frontmatterCharLength));
            }
            else if (kind is InstructionsFileSpanKind.PlainRule or InstructionsFileSpanKind.TaggedRule)
            {
                var id = kind == InstructionsFileSpanKind.TaggedRule ? ParseRuleId(span.Text.Span) : null;

                (rules ??= []).Add(new InstructionsFileRule(
                    id,
                    StripFinalLineTerminator(span.Text.Span),
                    new InstructionsFileLineSpan(
                        span.LineSpan.StartLine - frontmatterLineCount,
                        span.LineSpan.LineCount)));
            }
            else if (kind == InstructionsFileSpanKind.Reference)
            {
                if (span.ReferenceAddress is { } address)
                {
                    (references ??= []).Add(new InstructionsFileReference(
                        address,
                        new InstructionsFileTextSpan(
                            span.TextSpan.StartIndex - frontmatterCharLength,
                            span.TextSpan.Length),
                        span.LineSpan.StartLine - frontmatterLineCount));
                }
            }

            foreach (var diagnostic in span.Diagnostics)
            {
                (diagnostics ??= []).Add(diagnostic with
                {
                    Line = span.LineSpan.StartLine - frontmatterLineCount,
                });
            }
        }

        var content = rawContent.ToString();
        var body = content[frontmatterCharLength..];
        var sections = BuildSections(rawHeadings, body.Length);
        var version = name is null ? null : ExtractVersion(name);
        var applyTo = applyToRaw is null ? null : ApplyToParser.Parse(applyToRaw);

        var frontmatter = new InstructionsFileParsedFrontmatter(frontmatterRawValue, name, description, applyTo, version);
        var parsedBody = new InstructionsFileParsedBody(
            body,
            sections,
            rules ?? [],
            references ?? [],
            diagnostics ?? []);

        return new InstructionsFileParsedContent(content, frontmatter, parsedBody);

        void AssignFrontmatterField(FrontmatterField field, string value)
        {
            switch (field)
            {
                case FrontmatterField.Name:
                    name = value;
                    break;
                case FrontmatterField.Description:
                    description = value;
                    break;
                case FrontmatterField.ApplyTo:
                    applyToRaw = value;
                    break;
                case FrontmatterField.Unknown:
                default:
                    break;
            }
        }
    }

    /// <summary>
    /// Reads the instructions file at <paramref name="path"/> and builds its
    /// structured parse. This overload does the scanning for you: it runs an
    /// <see cref="InstructionsFileSpanParser"/> over the file in its default
    /// <see cref="InstructionsFileSpanEmitLevel.Full"/> / <see cref="InstructionsFileSpanEmitScope.All"/>
    /// configuration and passes the spans straight to <see cref="Parse"/>.
    /// </summary>
    /// <param name="path">The instructions file to read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The complete structural parse.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is
    /// <see langword="null"/>.</exception>
    public async Task<InstructionsFileParsedContent> ParseFileAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        var spanParser = _spanParser ??= new InstructionsFileSpanParser();
        var spans = await spanParser.ParseFileAsync(path, cancellationToken).ConfigureAwait(false);

        return Parse(spans);
    }

    private static List<InstructionsFileSection> BuildSections(List<RawHeading>? rawHeadings, int bodyLength)
    {
        if (rawHeadings is null)
        {
            return [];
        }

        var sections = new List<InstructionsFileSection>(rawHeadings.Count);

        for (var index = 0; index < rawHeadings.Count; index++)
        {
            var heading = rawHeadings[index];
            var charEnd = ComputeCharEnd(rawHeadings, index, bodyLength);
            var baseSlug = Slugify(heading.Text);
            var anchor = heading.Parent is null ? baseSlug : Slugify(heading.Parent) + "-" + baseSlug;

            sections.Add(new InstructionsFileSection(
                heading.Text,
                heading.Level,
                anchor,
                heading.Parent,
                new InstructionsFileTextSpan(heading.CharStart, charEnd - heading.CharStart)));
        }

        return sections;
    }

    private static FrontmatterField ClassifyFrontmatterKey(ReadOnlySpan<char> key)
        => key switch
        {
            "name" => FrontmatterField.Name,
            "description" => FrontmatterField.Description,
            "applyTo" => FrontmatterField.ApplyTo,
            _ => FrontmatterField.Unknown,
        };

    private static int ComputeCharEnd(IReadOnlyList<RawHeading> rawHeadings, int index, int bodyLength)
    {
        var current = rawHeadings[index];

        for (var next = index + 1; next < rawHeadings.Count; next++)
        {
            if (rawHeadings[next].Level <= current.Level)
            {
                return rawHeadings[next].CharStart;
            }
        }

        return bodyLength;
    }

    private static string? ExtractVersion(string name)
    {
        var match = GeneratedVersionSuffixRegex().Match(name);

        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex("^-+|-+$")]
    private static partial Regex GeneratedEdgeHyphensRegex();

    [GeneratedRegex(@"^---\r?\n([\s\S]*?)\r?\n---")]
    private static partial Regex GeneratedFrontmatterBlockRegex();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex GeneratedNonSlugRunRegex();

    [GeneratedRegex(@"\(v(\d+\.\d+\.\d+)\)")]
    private static partial Regex GeneratedVersionSuffixRegex();

    private static bool IsBlockKind(InstructionsFileSpanKind kind)
        => kind switch
        {
            InstructionsFileSpanKind.Text
                or InstructionsFileSpanKind.FrontmatterBlock
                or InstructionsFileSpanKind.Heading1
                or InstructionsFileSpanKind.Heading2
                or InstructionsFileSpanKind.Heading3
                or InstructionsFileSpanKind.PlainRule
                or InstructionsFileSpanKind.TaggedRule => true,
            InstructionsFileSpanKind.FrontmatterProperty
                or InstructionsFileSpanKind.FrontmatterKey
                or InstructionsFileSpanKind.FrontmatterValue
                or InstructionsFileSpanKind.Tag
                or InstructionsFileSpanKind.Reference => false,
            _ => false,
        };

    private static bool IsRuleTag(ReadOnlySpan<char> candidate)
    {
        if (candidate.Length != 8 || !candidate.StartsWith("INST", StringComparison.Ordinal))
        {
            return false;
        }

        for (var index = 4; index < candidate.Length; index++)
        {
            if (!char.IsDigit(candidate[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static string ParseHeadingText(ReadOnlySpan<char> headingLine)
    {
        var index = 0;

        while (index < headingLine.Length && headingLine[index] == '#')
        {
            index++;
        }

        return headingLine[index..].Trim().ToString();
    }

    private static string? ParseRuleId(ReadOnlySpan<char> ruleLine)
    {
        var open = ruleLine.IndexOf('[');

        if (open < 0)
        {
            return null;
        }

        var inner = ruleLine[(open + 1)..];
        var close = inner.IndexOf(']');

        if (close < 0)
        {
            return null;
        }

        var candidate = inner[..close];

        return IsRuleTag(candidate) ? candidate.ToString() : null;
    }

    [SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "Anchors are lowercase by GitHub/markdown convention; this is a display slug, not a security normalization.")]
    private static string Slugify(string heading)
    {
        var lowered = heading.ToLowerInvariant();
        var dashed = GeneratedNonSlugRunRegex().Replace(lowered, "-");

        return GeneratedEdgeHyphensRegex().Replace(dashed, string.Empty);
    }

    private static string StripFinalLineTerminator(ReadOnlySpan<char> text)
    {
        if (text.Length == 0 || text[^1] != '\n')
        {
            return text.ToString();
        }

        var length = text.Length - 1;

        if (length > 0 && text[length - 1] == '\r')
        {
            length--;
        }

        return text[..length].ToString();
    }

    private readonly record struct RawHeading(int Level, string Text, string? Parent, int CharStart);
}
