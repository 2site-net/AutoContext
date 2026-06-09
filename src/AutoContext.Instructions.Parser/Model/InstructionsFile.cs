namespace AutoContext.Instructions.Parser.Model;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

using AutoContext.Instructions.Parser.Syntax;

/// <summary>
/// A single instructions file in memory: its verbatim <see cref="RawContent"/>
/// together with the parsed <see cref="Frontmatter"/> and <see cref="Body"/> (the
/// body text, the <c>##</c>/<c>###</c> sections, the rule bullets, the
/// <c>[locator#fragment]</c> references, and any diagnostics). Everything that
/// reads instructions files — the build-time manifest generator and the runtime
/// engine — works from this one shape, so each file is parsed just once.
/// <para>
/// Construct one from a span stream with <see cref="FromSpans"/>, or read and parse
/// a file from disk with <see cref="InstructionsFileFactory.FromFileAsync"/>.
/// </para>
/// </summary>
/// <param name="RawContent">The exact file content, frontmatter and body
/// included.</param>
/// <param name="Frontmatter">The parsed frontmatter from the top of the file.</param>
/// <param name="Body">The parsed body: the body text plus its sections, rules, and
/// diagnostics.</param>
public sealed partial record InstructionsFile(
    string RawContent,
    InstructionsFileFrontmatter Frontmatter,
    InstructionsFileBody Body)
{
    private enum FrontmatterField
    {
        Unknown,
        Name,
        Description,
        ApplyTo,
    }

    /// <summary>
    /// Builds an <see cref="InstructionsFile"/> from a parsed
    /// <see cref="InstructionsFileSyntaxTree"/>. The tree must be the complete
    /// <see cref="InstructionsFileSpanEmitLevel.Full"/> /
    /// <see cref="InstructionsFileSpanEmitScope.All"/> output for a single file: the
    /// <see cref="InstructionsFileSyntaxTree.Frontmatter"/> stream supplies the
    /// frontmatter fields, the <see cref="InstructionsFileSyntaxTree.Body"/> stream
    /// supplies the headings and rule bullets, and the
    /// <see cref="InstructionsFileSyntaxTree.References"/> and
    /// <see cref="InstructionsFileSyntaxTree.Diagnostics"/> side streams supply the
    /// references and problems.
    /// <para>
    /// One thing it sorts out is positions. The spans count from the start of the
    /// file, frontmatter included, but callers expect positions measured from the
    /// start of the body, as if the frontmatter were not there. The leading
    /// <see cref="InstructionsFileSpanKind.FrontmatterBlock"/> span says how long the
    /// frontmatter is, and this method subtracts that from every offset before handing
    /// the result back.
    /// </para>
    /// </summary>
    /// <param name="tree">The parsed syntax tree.</param>
    /// <returns>The complete structural parse.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tree"/> is
    /// <see langword="null"/>.</exception>
    public static InstructionsFile FromSpans(InstructionsFileSyntaxTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);

        string? sourceText = null;
        var frontmatterCharLength = 0;
        var frontmatterLineCount = 0;
        var frontmatterRawValue = string.Empty;
        string? name = null;
        string? description = null;
        string? applyToRaw = null;
        var currentFrontmatterField = FrontmatterField.Unknown;

        foreach (var span in tree.Frontmatter)
        {
            // Every span is a window over the single source string the lexer
            // parsed, so recover that string from the first span rather than
            // rebuilding the file by concatenating spans.
            sourceText ??= RecoverSourceText(span);

            if (span.Kind == InstructionsFileSpanKind.FrontmatterBlock)
            {
                frontmatterCharLength = span.TextSpan.Length;
                frontmatterLineCount = span.LineSpan.LineCount;

                var block = GeneratedFrontmatterBlockRegex().Match(sourceText, 0, frontmatterCharLength);
                frontmatterRawValue = block.Success ? block.Groups[1].Value : string.Empty;
            }
            else if (span.Kind == InstructionsFileSpanKind.FrontmatterKey)
            {
                currentFrontmatterField = ClassifyFrontmatterKey(span.Text.Span);
                AssignFrontmatterField(currentFrontmatterField, string.Empty);
            }
            else if (span.Kind == InstructionsFileSpanKind.FrontmatterValue)
            {
                AssignFrontmatterField(currentFrontmatterField, span.Text.ToString());
            }
        }

        List<RawHeading>? rawHeadings = null;
        List<InstructionsFileRule>? rules = null;
        string? lastSectionHeading = null;

        foreach (var span in tree.Body)
        {
            sourceText ??= RecoverSourceText(span);
            var kind = span.Kind;

            if (kind is InstructionsFileSpanKind.Heading2 or InstructionsFileSpanKind.Heading3)
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
        }

        List<InstructionsFileReference>? references = null;

        foreach (var reference in tree.References)
        {
            (references ??= []).Add(new InstructionsFileReference(
                reference.Address,
                new InstructionsFileTextSpan(
                    reference.TextSpan.StartIndex - frontmatterCharLength,
                    reference.TextSpan.Length),
                reference.LineSpan.StartLine - frontmatterLineCount));
        }

        List<InstructionsFileDiagnostic>? diagnostics = null;

        foreach (var diagnostic in tree.Diagnostics)
        {
            (diagnostics ??= []).Add(diagnostic.Diagnostic with
            {
                Line = diagnostic.LineSpan.StartLine - frontmatterLineCount,
            });
        }

        var content = sourceText ?? string.Empty;
        var body = content[frontmatterCharLength..];
        var sections = BuildSections(rawHeadings, body.Length);
        var version = name is null ? null : ExtractVersion(name);
        var applyTo = applyToRaw is null ? null : FrontmatterApplyToParser.Parse(applyToRaw);

        var frontmatter = new InstructionsFileFrontmatter(frontmatterRawValue, name, description, applyTo, version);
        var parsedBody = new InstructionsFileBody(
            body,
            sections,
            rules ?? [],
            references ?? [],
            diagnostics ?? []);

        return new InstructionsFile(content, frontmatter, parsedBody);

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

    private static List<InstructionsFileSection> BuildSections(List<RawHeading>? rawHeadings, int bodyLength)
    {
        if (rawHeadings is null)
        {
            return [];
        }

        var sections = new List<InstructionsFileSection>(rawHeadings.Count);
        string? cachedParent = null;
        string? cachedParentSlug = null;

        for (var index = 0; index < rawHeadings.Count; index++)
        {
            var heading = rawHeadings[index];
            var charEnd = ComputeCharEnd(rawHeadings, index, bodyLength);
            var baseSlug = Slugify(heading.Text);
            string anchor;

            if (heading.Parent is null)
            {
                anchor = baseSlug;
            }
            else
            {
                if (!string.Equals(heading.Parent, cachedParent, StringComparison.Ordinal))
                {
                    cachedParent = heading.Parent;
                    cachedParentSlug = Slugify(heading.Parent);
                }

                anchor = cachedParentSlug + "-" + baseSlug;
            }

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

    private static string RecoverSourceText(InstructionsFileSyntaxSpan span)
        => MemoryMarshal.TryGetString(span.Text, out var text, out _, out _)
            ? text
            : span.Text.ToString();

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
