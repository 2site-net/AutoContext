namespace AutoContext.Instructions.Parser;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

/// <summary>
/// The single source of truth for reading an instruction file. One
/// <see cref="Parse"/> call walks the markdown once and returns everything a
/// consumer needs: the frontmatter, the normalised body, the <c>##</c>/<c>###</c>
/// section index, the actionable <c>**Do**</c>/<c>**Don't**</c> rule bullets, and
/// any bullet-tag diagnostics. Files with or without a frontmatter block, and
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
    /// section index, rule bullets, and diagnostics.
    /// </summary>
    /// <param name="content">The full instruction file text.</param>
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
    /// <param name="content">The full instruction file text.</param>
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

    [GeneratedRegex(@"^(#{2,3}) +(.+?)\s*$")]
    private static partial Regex GeneratedHeadingLineRegex();

    [GeneratedRegex(@"^[-*]\s\[(?!INST\d{4}\])[^\]]*\]\s*\*\*(Do|Don't)\*\*")]
    private static partial Regex GeneratedMalformedRuleBulletRegex();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex GeneratedNonSlugRunRegex();

    [GeneratedRegex(@"^[-*]\s(?:\[(INST\d{4})\]\s*)?\*\*(Do|Don't)\*\*")]
    private static partial Regex GeneratedRuleBulletRegex();

    [GeneratedRegex(@"\(v(\d+\.\d+\.\d+)\)")]
    private static partial Regex GeneratedVersionSuffixRegex();

    private static InstructionsFileBodyParsedResult ParseBody(string body)
    {
        var bodySpan = body.AsSpan();
        var rawHeadings = new List<RawHeading>();
        var rules = new List<InstructionsFileRule>();
        var diagnostics = new List<InstructionsFileDiagnostic>();
        var seenIds = new Dictionary<string, int>(StringComparer.Ordinal);

        var inFence = false;
        string? lastSectionHeading = null;

        var ruleStart = -1;
        var ruleLines = new List<string>();
        string? ruleId = null;
        var lineIndex = 0;

        // Each line feeds two independent scans that share no state: a
        // fence-aware section scan (headings inside a fenced code block are
        // literal text, not structure) and a fence-agnostic rule scan (rule
        // bullets are recognised everywhere, including inside fences).

        foreach (var lineRange in bodySpan.Split('\n'))
        {
            var line = bodySpan[lineRange];

            if (GeneratedFenceLineRegex().IsMatch(line))
            {
                inFence = !inFence;
            }
            else if (!inFence && GeneratedHeadingLineRegex().IsMatch(line))
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

        return new InstructionsFileBodyParsedResult(body, sections, rules, diagnostics);
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

    private readonly record struct RawHeading(int Level, string Heading, int CharStart, string? Parent);
}
