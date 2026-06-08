namespace AutoContext.Instructions.Parser;

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Rebuilds the structured <see cref="InstructionsFileParsedContent"/> from the
/// flat span stream emitted by <see cref="InstructionsFileSpanParser"/> in
/// <see cref="InstructionsFileSpanEmitLevel.Full"/> / <see cref="InstructionsFileSpanEmitScope.All"/>
/// mode. The span parser is the lexer — a gapless block partition with nested
/// token spans addressed by whole-file coordinates; this structured parser is the
/// structuring pass that turns that stream into the frontmatter, the
/// <c>##</c>/<c>###</c> section index, the rule bullets, the
/// <c>[locator#fragment]</c> references, and the file-local diagnostics consumers
/// read.
/// <para>
/// Two coordinate systems meet here. The spans carry whole-file offsets that count
/// the frontmatter block; the structured body addresses everything relative to the
/// frontmatter-stripped body, exactly as a consumer expects. The leading
/// <see cref="InstructionsFileSpanKind.FrontmatterBlock"/> span supplies the
/// character and line lengths that translate one into the other.
/// </para>
/// </summary>
internal sealed partial class InstructionsFileStructuredParser
{
    private InstructionsFileSpanParser? _spanParser;

    /// <summary>
    /// Rebuilds the structured parse from a <see cref="InstructionsFileSpanParser"/>
    /// span stream. The spans must be the complete <see cref="InstructionsFileSpanEmitLevel.Full"/> /
    /// <see cref="InstructionsFileSpanEmitScope.All"/> decomposition of one file, in
    /// document order — the block partition supplies the verbatim content while the
    /// nested token spans supply the frontmatter fields, tags, and references. The
    /// stream is consumed lazily in a single forward pass; no intermediate list is
    /// materialised.
    /// </summary>
    /// <param name="spans">The span stream.</param>
    /// <param name="cancellationToken">Cancels the enumeration.</param>
    /// <returns>The complete structural parse.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="spans"/> is
    /// <see langword="null"/>.</exception>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Kept an instance method so the parser presents one coherent instance API surface alongside the stateful ParseFileAsync.")]
    public async Task<InstructionsFileParsedContent> ParseAsync(
        IAsyncEnumerable<InstructionsFileParsedSpan> spans,
        CancellationToken cancellationToken = default)
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

        await foreach (var span in spans.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            var kind = span.Kind;

            if (IsBlockKind(kind))
            {
                rawContent.Append(span.Text);
            }

            if (kind == InstructionsFileSpanKind.FrontmatterBlock)
            {
                frontmatterCharLength = span.TextSpan.Length;
                frontmatterLineCount = span.LineSpan.LineCount;

                var block = GeneratedFrontmatterBlockRegex().Match(span.Text);
                frontmatterRawValue = block.Success ? block.Groups[1].Value : string.Empty;
            }
            else if (kind == InstructionsFileSpanKind.FrontmatterProperty)
            {
                var field = GeneratedFrontmatterFieldRegex().Match(span.Text);

                if (field.Success)
                {
                    var key = field.Groups[1].Value;

                    if (key == "name")
                    {
                        name = field.Groups[2].Value;
                    }
                    else if (key == "description")
                    {
                        description = field.Groups[2].Value;
                    }
                    else if (key == "applyTo")
                    {
                        applyToRaw = field.Groups[2].Value;
                    }
                }
            }
            else if (kind is InstructionsFileSpanKind.Heading2 or InstructionsFileSpanKind.Heading3)
            {
                var level = kind == InstructionsFileSpanKind.Heading2 ? 2 : 3;
                var text = ParseHeadingText(span.Text);
                var parent = level == 2 ? null : lastSectionHeading;

                if (level == 2)
                {
                    lastSectionHeading = text;
                }

                (rawHeadings ??= []).Add(new RawHeading(level, text, parent, span.TextSpan.StartIndex - frontmatterCharLength));
            }
            else if (kind is InstructionsFileSpanKind.PlainRule or InstructionsFileSpanKind.TaggedRule)
            {
                var id = kind == InstructionsFileSpanKind.TaggedRule ? ParseRuleId(span.Text) : null;

                (rules ??= []).Add(new InstructionsFileRule(
                    id,
                    StripFinalLineTerminator(span.Text),
                    span.LineSpan.StartLine - frontmatterLineCount,
                    span.LineSpan.EndLine - 1 - frontmatterLineCount));
            }
            else if (kind == InstructionsFileSpanKind.Reference)
            {
                var reference = ParseReference(span, frontmatterCharLength, frontmatterLineCount);

                if (reference is not null)
                {
                    (references ??= []).Add(reference);
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
    }

    /// <summary>
    /// Reads the instructions file at <paramref name="path"/> and rebuilds its
    /// structured parse. This overload owns the lex: it runs an
    /// <see cref="InstructionsFileSpanParser"/> over the file in its default
    /// <see cref="InstructionsFileSpanEmitLevel.Full"/> / <see cref="InstructionsFileSpanEmitScope.All"/>
    /// configuration and feeds the span stream straight into <see cref="ParseAsync"/>.
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

        return await ParseAsync(spanParser.ParseFileAsync(path, cancellationToken), cancellationToken).ConfigureAwait(false);
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
                heading.CharStart,
                charEnd));
        }

        return sections;
    }

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

    [GeneratedRegex("^(\\w+):\\s*\"?([^\"\\r\\n]*)\"?\\s*$")]
    private static partial Regex GeneratedFrontmatterFieldRegex();

    [GeneratedRegex(@"\\(.)")]
    private static partial Regex GeneratedHeadingEscapeRegex();

    [GeneratedRegex(@"^(?:\.{1,2}/)?(?:[^/\s]+/)*[^/\s]+\.instructions\.md$")]
    private static partial Regex GeneratedLocatorFileRegex();

    [GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex GeneratedLocatorKeyRegex();

    [GeneratedRegex(@"^[a-z][a-z0-9+.-]*://\S+$")]
    private static partial Regex GeneratedLocatorUriRegex();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex GeneratedNonSlugRunRegex();

    [GeneratedRegex(@"^INST\d{4}$")]
    private static partial Regex GeneratedReferenceRuleFragmentRegex();

    [GeneratedRegex(@"^'(?:[^'\\]|\\.)+'$")]
    private static partial Regex GeneratedReferenceSectionFragmentRegex();

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

    private static bool IsValidLocator(ReadOnlySpan<char> locator)
        => GeneratedLocatorKeyRegex().IsMatch(locator)
        || GeneratedLocatorFileRegex().IsMatch(locator)
        || GeneratedLocatorUriRegex().IsMatch(locator);

    private static string ParseHeadingText(ReadOnlySpan<char> headingLine)
    {
        var index = 0;

        while (index < headingLine.Length && headingLine[index] == '#')
        {
            index++;
        }

        return headingLine[index..].Trim().ToString();
    }

    private static InstructionsFileReference? ParseReference(
        InstructionsFileParsedSpan span,
        int frontmatterCharLength,
        int frontmatterLineCount)
    {
        var token = span.Text;
        var inner = token.AsSpan(1, token.Length - 2);
        var separator = inner.IndexOf('#');

        if (separator < 0)
        {
            return null;
        }

        var locator = inner[..separator];
        var fragment = inner[(separator + 1)..];
        var hasLocator = locator.Length > 0;

        // A bad locator is reported by the span's own MalformedReference diagnostic;
        // it yields no structured reference.
        if (hasLocator && !IsValidLocator(locator))
        {
            return null;
        }

        var line = span.LineSpan.StartLine - frontmatterLineCount;
        var charStart = span.TextSpan.StartIndex - frontmatterCharLength;
        var charEnd = span.TextSpan.EndIndex - frontmatterCharLength;

        if (GeneratedReferenceRuleFragmentRegex().IsMatch(fragment))
        {
            return new InstructionsFileReference(
                InstructionsFileReferenceKind.Rule,
                hasLocator ? locator.ToString() : null,
                fragment.ToString(),
                line,
                charStart,
                charEnd);
        }

        if (GeneratedReferenceSectionFragmentRegex().IsMatch(fragment))
        {
            return new InstructionsFileReference(
                InstructionsFileReferenceKind.Section,
                hasLocator ? locator.ToString() : null,
                UnescapeHeading(fragment[1..^1].ToString()),
                line,
                charStart,
                charEnd);
        }

        // A malformed fragment is likewise carried by the span diagnostic, not as a
        // structured reference.
        return null;
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

    private static string UnescapeHeading(string heading)
        => heading.Contains('\\', StringComparison.Ordinal)
            ? GeneratedHeadingEscapeRegex().Replace(heading, "$1")
            : heading;

    private readonly record struct RawHeading(int Level, string Text, string? Parent, int CharStart);
}
