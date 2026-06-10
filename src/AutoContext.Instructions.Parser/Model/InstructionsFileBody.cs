namespace AutoContext.Instructions.Parser.Model;

using System.Text;

using AutoContext.Instructions.Parser.Syntax;

/// <summary>
/// The body part of an <see cref="InstructionsFile"/>: the body text that the
/// offsets are measured against, plus the structure found in a single walk over
/// it — the <c>##</c>/<c>###</c> sections and the <c>**Do**</c>/<c>**Don't**</c>
/// rule bullets. The prose references and diagnostics gathered in the same walk
/// live on the owning <see cref="InstructionsFile"/>, not here. The parser fills
/// this in one pass and pairs it with the frontmatter.
/// </summary>
/// <param name="RawValue">The body text: the file with its leading frontmatter
/// block removed. All section and rule offsets are measured from the start of this
/// text.</param>
/// <param name="Sections">The <c>##</c>/<c>###</c> sections, in the order they
/// appear.</param>
/// <param name="Rules">The <c>**Do**</c>/<c>**Don't**</c> rule bullets, in the
/// order they appear.</param>
public sealed record InstructionsFileBody(
    string RawValue,
    IReadOnlyList<InstructionsFileSection> Sections,
    IReadOnlyList<InstructionsFileRule> Rules)
{
    /// <summary>
    /// The parser used to rebuild a body from its text once rules have been
    /// removed (see <see cref="WithoutTaggedRules"/>). The reparse input is a body
    /// with no frontmatter, so <see cref="InstructionsFileSpanEmitScope.Body"/>
    /// emits everything the rebuild reads; diagnostics are off because a body
    /// carries none. Shared and stateless across calls.
    /// </summary>
    private static readonly InstructionsFileSyntaxParser BodyReparser =
        new(emitScope: InstructionsFileSpanEmitScope.Body, includeDiagnostics: false);

    /// <summary>
    /// Builds an <see cref="InstructionsFileBody"/> from the body span stream of a
    /// parsed file. The body spans supply the <see cref="RawValue"/>,
    /// <see cref="Sections"/>, and <see cref="Rules"/>.
    /// <para>
    /// Positions are reported from the start of the body. Each body span already
    /// carries its body-relative <see cref="InstructionsFileSyntaxSpan.Offset"/>, so
    /// this method works the same whether it is given the body part of a whole file
    /// or a frontmatter-free reparse — no origin has to be passed in.
    /// </para>
    /// </summary>
    /// <param name="bodySpans">The body spans, in document order.</param>
    /// <returns>The parsed body.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bodySpans"/> is
    /// <see langword="null"/>.</exception>
    public static InstructionsFileBody FromSpans(IReadOnlyList<InstructionsFileSyntaxSpan> bodySpans)
    {
        ArgumentNullException.ThrowIfNull(bodySpans);

        var rawValue = string.Empty;

        if (bodySpans.Count > 0)
        {
            // The first body span is a block at the body origin, so the difference
            // between its absolute and body-relative offsets recovers the body origin.
            // Slicing the recovered source there yields the body text with the
            // frontmatter removed (and the whole text when there is no frontmatter,
            // where the origin is zero). Every body span carries an offset.
            var first = bodySpans[0];
            var firstOffset = first.Offset ?? throw MissingOffset();
            var charOrigin = first.TextSpan.StartIndex - firstOffset.StartIndex;
            rawValue = first.RecoverSourceText()[charOrigin..];
        }

        List<RawHeading>? rawHeadings = null;
        List<InstructionsFileRule>? rules = null;
        string? lastSectionHeading = null;

        foreach (var span in bodySpans)
        {
            var kind = span.Kind;
            var offset = span.Offset ?? throw MissingOffset();

            if (kind is InstructionsFileSpanKind.Heading2 or InstructionsFileSpanKind.Heading3)
            {
                var level = kind == InstructionsFileSpanKind.Heading2 ? 2 : 3;
                var text = ParseHeadingText(span.Text.Span);
                var parent = level == 2 ? null : lastSectionHeading;

                if (level == 2)
                {
                    lastSectionHeading = text;
                }

                (rawHeadings ??= []).Add(new RawHeading(level, text, parent, offset.StartIndex));
            }
            else if (kind is InstructionsFileSpanKind.PlainRule or InstructionsFileSpanKind.TaggedRule)
            {
                var id = kind == InstructionsFileSpanKind.TaggedRule ? ParseRuleId(span.Text.Span) : null;

                (rules ??= []).Add(new InstructionsFileRule(
                    id,
                    StripFinalLineTerminator(span.Text.Span),
                    new InstructionsFileLineSpan(offset.StartLine, span.LineSpan.LineCount)));
            }
        }

        var sections = BuildSections(rawHeadings, rawValue.Length);

        return new InstructionsFileBody(rawValue, sections, rules ?? []);
    }

    /// <summary>
    /// Returns a body with the tagged rules whose ids are in
    /// <paramref name="ruleIds"/> removed. The matched rules' lines are deleted
    /// from the body text and the shortened text is parsed again, so the returned
    /// body's <see cref="RawValue"/>, <see cref="Sections"/>, and
    /// <see cref="Rules"/> all describe what remains — every offset and line
    /// number is measured against the shortened text. Rules with no id, and ids
    /// not present in this body, are left untouched; when nothing matches the same
    /// instance is returned.
    /// </summary>
    /// <param name="ruleIds">The <c>INST####</c> ids of the rules to drop.</param>
    /// <param name="cancellationToken">Cancels the reparse.</param>
    /// <returns>A body without the named rules, or this same body when none
    /// matched.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ruleIds"/> is
    /// <see langword="null"/>.</exception>
    public InstructionsFileBody WithoutTaggedRules(
        IReadOnlySet<string> ruleIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ruleIds);

        if (ruleIds.Count == 0)
        {
            return this;
        }

        List<InstructionsFileLineSpan>? removed = null;

        foreach (var rule in Rules)
        {
            if (rule.Id is { } id && ruleIds.Contains(id))
            {
                (removed ??= []).Add(rule.LineSpan);
            }
        }

        if (removed is null)
        {
            return this;
        }

        var shortened = RemoveLines(RawValue, removed);
        var tree = BodyReparser.Parse(shortened, cancellationToken);

        return FromSpans(tree.Body);
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
            var baseSlug = InstructionsFileUtils.Slugify(heading.Text);
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
                    cachedParentSlug = InstructionsFileUtils.Slugify(heading.Parent);
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

    private static bool IsExcluded(int lineIndex, List<InstructionsFileLineSpan> excludedLineSpans)
    {
        foreach (var span in excludedLineSpans)
        {
            if (lineIndex >= span.StartLine && lineIndex < span.EndLine)
            {
                return true;
            }
        }

        return false;
    }

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

    private static InvalidOperationException MissingOffset()
        => new("A body span must carry an offset, but one was null.");

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

    private static string RemoveLines(string rawValue, List<InstructionsFileLineSpan> excludedLineSpans)
    {
        ReadOnlySpan<char> body = rawValue;
        var builder = new StringBuilder(rawValue.Length);
        var lineStart = 0;
        var lineIndex = 0;
        var wroteLine = false;

        while (true)
        {
            var newlineOffset = body[lineStart..].IndexOf('\n');
            var lineEnd = newlineOffset < 0 ? body.Length : lineStart + newlineOffset;

            if (!IsExcluded(lineIndex, excludedLineSpans))
            {
                if (wroteLine)
                {
                    builder.Append('\n');
                }

                builder.Append(body[lineStart..lineEnd]);
                wroteLine = true;
            }

            if (newlineOffset < 0)
            {
                break;
            }

            lineStart = lineEnd + 1;
            lineIndex++;
        }

        return builder.ToString();
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
