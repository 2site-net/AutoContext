namespace AutoContext.Instructions.Parser;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

/// <summary>
/// The single source of truth for reading an instructions file. One
/// <see cref="Parse"/> call walks the markdown once and returns everything a
/// consumer needs: the frontmatter, the normalised body, the <c>##</c>/<c>###</c>
/// section index, the actionable <c>**Do**</c>/<c>**Don't**</c> rule bullets, the
/// bare <c>[locator#fragment]</c> cross-references, and any bullet-tag or
/// reference diagnostics. Files with or without a frontmatter block, and
/// with or without <c>INST####</c> tags, all parse cleanly; the parser never
/// throws on content shape and never validates curatorial rules — it reports and
/// returns, leaving fatality decisions to the build-time generators and the
/// runtime engine that share it.
/// </summary>
public static partial class InstructionsFileParser
{
    private const int HeadingLevelSection = 2;

    /// <summary>
    /// Parses <paramref name="content"/> into its frontmatter, normalised body,
    /// section index, rule bullets, references, and diagnostics.
    /// </summary>
    /// <param name="content">The full instructions file text.</param>
    /// <returns>The complete structural parse.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is
    /// <see langword="null"/>.</exception>
    public static InstructionsFileParsedResult Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var frontmatter = ParseFrontmatter(content);
        var body = GeneratedFrontmatterStripRegex().Replace(content, string.Empty);
        var parsedBody = ParseBody(body);

        return new InstructionsFileParsedResult(frontmatter, parsedBody);
    }

    /// <summary>
    /// Parses only the leading frontmatter block of <paramref name="content"/> —
    /// the cheap path for consumers that need the catalogue fields but not the
    /// section or rule index.
    /// </summary>
    /// <param name="content">The full instructions file text.</param>
    /// <returns>The parsed frontmatter; all fields are <see langword="null"/>
    /// when no frontmatter block is present.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is
    /// <see langword="null"/>.</exception>
    public static InstructionsFileFrontmatterParsedResult ParseFrontmatter(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var block = GeneratedFrontmatterBlockRegex().Match(content);

        if (!block.Success)
        {
            return new InstructionsFileFrontmatterParsedResult(null, null, null, null);
        }

        string? name = null;
        string? description = null;
        string? applyToRaw = null;

        var blockValue = block.Groups[1].ValueSpan;

        foreach (var lineRange in blockValue.Split('\n'))
        {
            var line = blockValue[lineRange].Trim();

            if (line.IsEmpty)
            {
                continue;
            }

            var field = GeneratedFrontmatterFieldRegex().Match(line.ToString());

            if (!field.Success)
            {
                continue;
            }

            switch (field.Groups[1].Value)
            {
                case "name":
                    name = field.Groups[2].Value;
                    break;
                case "description":
                    description = field.Groups[2].Value;
                    break;
                case "applyTo":
                    applyToRaw = field.Groups[2].Value;
                    break;
                default:
                    break;
            }
        }

        var version = name is null ? null : ExtractVersion(name);
        var applyTo = applyToRaw is null ? null : ApplyToParser.Parse(applyToRaw);

        return new InstructionsFileFrontmatterParsedResult(name, description, applyTo, version);
    }

    private static InstructionsFileRule BuildRule(
        string? id,
        List<string> lines,
        int startLine,
        int endLine)
    {
        var end = lines.Count;

        while (end > 0 && lines[end - 1].AsSpan().Trim().IsEmpty)
        {
            end--;
            endLine--;
        }

        var text = string.Join('\n', CollectionsMarshal.AsSpan(lines)[..end]);

        return new InstructionsFileRule(id, text, startLine, endLine);
    }

    private static List<InstructionsFileSection> BuildSections(IReadOnlyList<RawHeading> rawHeadings, int bodyLength)
    {
        var sections = new List<InstructionsFileSection>(rawHeadings.Count);

        for (var index = 0; index < rawHeadings.Count; index++)
        {
            var heading = rawHeadings[index];
            var charEnd = ComputeCharEnd(rawHeadings, index, bodyLength);
            var baseSlug = Slugify(heading.Heading);
            var anchor = heading.Parent is null ? baseSlug : Slugify(heading.Parent) + "-" + baseSlug;

            sections.Add(new InstructionsFileSection(
                heading.Heading,
                heading.Level,
                anchor,
                heading.Parent,
                heading.CharStart,
                charEnd));
        }

        return sections;
    }

    private static int ComputeCharEnd(IReadOnlyList<RawHeading> raw, int index, int bodyLength)
    {
        var current = raw[index];

        for (var next = index + 1; next < raw.Count; next++)
        {
            if (raw[next].Level <= current.Level)
            {
                return raw[next].CharStart;
            }
        }

        return bodyLength;
    }

    private static string? ExtractVersion(string name)
    {
        var match = GeneratedVersionSuffixRegex().Match(name);

        return match.Success ? match.Groups[1].Value : null;
    }

    private static int FindClosingBacktickRun(char[] buffer, int start, int fence)
    {
        var index = start;

        while (index < buffer.Length)
        {
            if (buffer[index] != '`')
            {
                index++;
                continue;
            }

            var run = index;
            var length = 0;

            while (index < buffer.Length && buffer[index] == '`')
            {
                length++;
                index++;
            }

            if (length == fence)
            {
                return run;
            }
        }

        return -1;
    }

    [GeneratedRegex(@"\[([^\]]*)\]")]
    private static partial Regex GeneratedBracketTagRegex();

    [GeneratedRegex("^-+|-+$")]
    private static partial Regex GeneratedEdgeHyphensRegex();

    [GeneratedRegex("^```")]
    private static partial Regex GeneratedFenceLineRegex();

    [GeneratedRegex(@"^---\r?\n([\s\S]*?)\r?\n---")]
    private static partial Regex GeneratedFrontmatterBlockRegex();

    [GeneratedRegex("^(\\w+):\\s*\"?([^\"\\r\\n]*)\"?\\s*$")]
    private static partial Regex GeneratedFrontmatterFieldRegex();

    [GeneratedRegex(@"^---\r?\n[\s\S]*?\r?\n---\r?\n?")]
    private static partial Regex GeneratedFrontmatterStripRegex();

    [GeneratedRegex(@"\\(.)")]
    private static partial Regex GeneratedHeadingEscapeRegex();

    [GeneratedRegex(@"^(#{2,3}) +(.+?)\s*$")]
    private static partial Regex GeneratedHeadingLineRegex();

    [GeneratedRegex(@"^(?:\.{1,2}/)?(?:[^/\s]+/)*[^/\s]+\.instructions\.md$")]
    private static partial Regex GeneratedLocatorFileRegex();

    [GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex GeneratedLocatorKeyRegex();

    [GeneratedRegex(@"^[a-z][a-z0-9+.-]*://\S+$")]
    private static partial Regex GeneratedLocatorUriRegex();

    [GeneratedRegex(@"^[-*]\s\[(?!INST\d{4}\])[^\]]*\]\s*\*\*(Do|Don't)\*\*")]
    private static partial Regex GeneratedMalformedRuleBulletRegex();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex GeneratedNonSlugRunRegex();

    [GeneratedRegex(@"^INST\d{4}\s*[-–/]")]
    private static partial Regex GeneratedReferenceRangeFragmentRegex();

    [GeneratedRegex(@"^INST\d{4}$")]
    private static partial Regex GeneratedReferenceRuleFragmentRegex();

    [GeneratedRegex(@"^'(?:[^'\\]|\\.)+'$")]
    private static partial Regex GeneratedReferenceSectionFragmentRegex();

    [GeneratedRegex(@"\[([^\[\]#]*)#([^\[\]]*)\](?![(\[:])")]
    private static partial Regex GeneratedReferenceTokenRegex();

    [GeneratedRegex(@"^[-*]\s(?:\[(INST\d{4})\]\s*)?\*\*(Do|Don't)\*\*")]
    private static partial Regex GeneratedRuleBulletRegex();

    [GeneratedRegex(@"\(v(\d+\.\d+\.\d+)\)")]
    private static partial Regex GeneratedVersionSuffixRegex();

    private static bool IsValidLocator(string locator)
        => GeneratedLocatorKeyRegex().IsMatch(locator)
        || GeneratedLocatorFileRegex().IsMatch(locator)
        || GeneratedLocatorUriRegex().IsMatch(locator);

    private static string MaskInlineCode(ReadOnlySpan<char> line)
    {
        if (!line.Contains('`'))
        {
            return line.ToString();
        }

        var buffer = line.ToArray();
        var index = 0;

        while (index < buffer.Length)
        {
            if (buffer[index] != '`')
            {
                index++;
                continue;
            }

            var open = index;
            var fence = 0;

            while (index < buffer.Length && buffer[index] == '`')
            {
                fence++;
                index++;
            }

            var close = FindClosingBacktickRun(buffer, index, fence);

            if (close < 0)
            {
                // No matching closing run — the remaining backtick is literal text,
                // not a code span, so nothing further on the line is masked.
                break;
            }

            // Blank the whole span, delimiters included, so bracketed examples
            // inside it cannot be mistaken for references; length is preserved so
            // offsets into the original line stay valid.
            Array.Fill(buffer, ' ', open, close + fence - open);
            index = close + fence;
        }

        return new string(buffer);
    }

    private static InstructionsFileBodyParsedResult ParseBody(string body)
    {
        var bodySpan = body.AsSpan();
        var rawHeadings = new List<RawHeading>();
        var rules = new List<InstructionsFileRule>();
        var references = new List<InstructionsFileReference>();
        var diagnostics = new List<InstructionsFileDiagnostic>();
        var seenIds = new Dictionary<string, int>(StringComparer.Ordinal);

        var inFence = false;
        string? lastSectionHeading = null;

        var ruleStart = -1;
        var ruleLines = new List<string>();
        string? ruleId = null;
        var lineIndex = 0;

        // Each line feeds independent scans that share no state: a fence-aware
        // section-and-reference scan (headings and bare [domain#INST0001]
        // references inside a fenced code block are literal text, not structure)
        // and a fence-agnostic rule scan (rule bullets are recognised everywhere,
        // including inside fences).

        foreach (var lineRange in bodySpan.Split('\n'))
        {
            var line = bodySpan[lineRange];

            if (GeneratedFenceLineRegex().IsMatch(line))
            {
                inFence = !inFence;
            }
            else if (!inFence)
            {
                if (GeneratedHeadingLineRegex().IsMatch(line))
                {
                    var heading = GeneratedHeadingLineRegex().Match(line.ToString());
                    var level = heading.Groups[1].Value.Length;
                    var text = heading.Groups[2].Value.Trim();

                    if (level == HeadingLevelSection)
                    {
                        rawHeadings.Add(new RawHeading(level, text, lineRange.Start.Value, null));
                        lastSectionHeading = text;
                    }
                    else
                    {
                        rawHeadings.Add(new RawHeading(level, text, lineRange.Start.Value, lastSectionHeading));
                    }
                }

                // Reference tokens are fence-aware: a bare [domain#INST0001] inside
                // a code fence is a syntax example, not a live reference.
                ScanReferences(line, lineRange.Start.Value, lineIndex, references, diagnostics);
            }

            if (GeneratedRuleBulletRegex().IsMatch(line))
            {
                if (ruleStart >= 0)
                {
                    rules.Add(BuildRule(ruleId, ruleLines, ruleStart, lineIndex - 1));
                }

                var bullet = GeneratedRuleBulletRegex().Match(line.ToString());

                ruleId = bullet.Groups[1].Success ? bullet.Groups[1].Value : null;
                ruleStart = lineIndex;
                ruleLines = [line.ToString()];

                if (ruleId is null)
                {
                    diagnostics.Add(new InstructionsFileDiagnostic(
                        InstructionsFileDiagnosticKind.MissingId,
                        lineIndex,
                        "Instruction has no ID (unfilterable)"));
                }
                else if (seenIds.TryGetValue(ruleId, out var firstLine))
                {
                    diagnostics.Add(new InstructionsFileDiagnostic(
                        InstructionsFileDiagnosticKind.DuplicateId,
                        lineIndex,
                        $"Duplicate instruction ID: {ruleId} (first seen at line {firstLine + 1})"));
                }
                else
                {
                    seenIds[ruleId] = lineIndex;
                }
            }
            else if (GeneratedMalformedRuleBulletRegex().IsMatch(line))
            {
                var bracket = GeneratedBracketTagRegex().Match(line.ToString()).Groups[1].Value;

                diagnostics.Add(new InstructionsFileDiagnostic(
                    InstructionsFileDiagnosticKind.MalformedId,
                    lineIndex,
                    $"Malformed instruction ID: [{bracket}]"));
            }
            else if (ruleStart >= 0)
            {
                if (line.IsEmpty || char.IsWhiteSpace(line[0]))
                {
                    ruleLines.Add(line.ToString());
                }
                else
                {
                    rules.Add(BuildRule(ruleId, ruleLines, ruleStart, lineIndex - 1));
                    ruleStart = -1;
                    ruleLines = [];
                    ruleId = null;
                }
            }

            lineIndex++;
        }

        if (ruleStart >= 0)
        {
            rules.Add(BuildRule(ruleId, ruleLines, ruleStart, lineIndex - 1));
        }

        var sections = BuildSections(rawHeadings, body.Length);

        return new InstructionsFileBodyParsedResult(body, sections, rules, references, diagnostics);
    }

    private static void ScanReferences(
        ReadOnlySpan<char> line,
        int lineStart,
        int lineIndex,
        List<InstructionsFileReference> references,
        List<InstructionsFileDiagnostic> diagnostics)
    {
        // The mandatory '#' is the cheapest disqualifier: a line without one
        // cannot hold a reference token.
        if (!line.Contains('#'))
        {
            return;
        }

        var masked = MaskInlineCode(line);

        foreach (Match match in GeneratedReferenceTokenRegex().Matches(masked))
        {
            var locator = match.Groups[1].Value;
            var fragment = match.Groups[2].Value;
            var hasLocator = locator.Length > 0;
            var locatorValid = !hasLocator || IsValidLocator(locator);
            var fragmentLooksReference = fragment.StartsWith("INST", StringComparison.Ordinal)
                || (fragment.Length > 0 && fragment[0] == '\'');

            // Only treat the token as a reference *attempt* when either the locator
            // looks deliberate or the fragment opens like an id/section — otherwise
            // it is ordinary bracketed prose and must be left alone.

            if (!((hasLocator && locatorValid) || fragmentLooksReference))
            {
                continue;
            }

            var charStart = lineStart + match.Index;
            var charEnd = charStart + match.Length;

            if (hasLocator && !locatorValid)
            {
                diagnostics.Add(new InstructionsFileDiagnostic(
                    InstructionsFileDiagnosticKind.MalformedReference,
                    lineIndex,
                    $"Malformed reference locator: [{locator}#{fragment}]"));
            }
            else if (GeneratedReferenceRuleFragmentRegex().IsMatch(fragment))
            {
                references.Add(new InstructionsFileReference(
                    InstructionsFileReferenceKind.Rule,
                    hasLocator ? locator : null,
                    fragment,
                    lineIndex,
                    charStart,
                    charEnd));
            }
            else if (GeneratedReferenceSectionFragmentRegex().IsMatch(fragment))
            {
                references.Add(new InstructionsFileReference(
                    InstructionsFileReferenceKind.Section,
                    hasLocator ? locator : null,
                    UnescapeHeading(fragment[1..^1]),
                    lineIndex,
                    charStart,
                    charEnd));
            }
            else
            {
                var message = GeneratedReferenceRangeFragmentRegex().IsMatch(fragment)
                    ? $"Reference ranges are not allowed; cite each rule individually or the enclosing section: [{locator}#{fragment}]"
                    : $"Malformed reference fragment: [{locator}#{fragment}]";

                diagnostics.Add(new InstructionsFileDiagnostic(
                    InstructionsFileDiagnosticKind.MalformedReference,
                    lineIndex,
                    message));
            }
        }
    }

    [SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "Anchors are lowercase by GitHub/markdown convention; this is a display slug, not a security normalization.")]
    internal static string Slugify(string heading)
    {
        var lowered = heading.ToLowerInvariant();
        var dashed = GeneratedNonSlugRunRegex().Replace(lowered, "-");

        return GeneratedEdgeHyphensRegex().Replace(dashed, string.Empty);
    }

    // Resolve markdown backslash escapes (e.g. \' → ') inside a quoted section
    // heading so the stored target is the literal heading text. Matches are
    // non-overlapping left-to-right, so \\ collapses to a single backslash before
    // a following \' is considered.
    private static string UnescapeHeading(string heading)
        => heading.Contains('\\', StringComparison.Ordinal)
            ? GeneratedHeadingEscapeRegex().Replace(heading, "$1")
            : heading;

    private readonly record struct RawHeading(int Level, string Heading, int CharStart, string? Parent);
}
