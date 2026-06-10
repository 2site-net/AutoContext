namespace AutoContext.Engine.Core.Features.Instructions;

using System.Text;

using AutoContext.Engine.Core.Features.Instructions.Snapshot;
using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;

using AutoContext.Instructions.Parser;
using AutoContext.Instructions.Parser.Model;
using AutoContext.Instructions.Parser.Syntax;

/// <summary>
/// Projects one instruction file's body per request for the
/// <c>Instructions.Get</c> family of RPCs. Resolves the body source
/// (a workspace override shadows the bundled copy), reads and parses it,
/// filters out the rules disabled in <c>.autocontext.json</c>, and slices
/// the result down to the requested sections.
/// </summary>
/// <remarks>
/// Two projection steps from the corpus build are deliberately kept here:
/// the leading frontmatter block is stripped (the parser's normalised body
/// already excludes it), and rules the workspace has disabled are removed.
/// The <c>[INSTxxxx]</c> tags on the surviving rules are <b>not</b>
/// stripped — the id is the anchor a cross-rule or cross-file
/// <c>[locator#fragment]</c> reference resolves to, so removing it would
/// leave references pointing at content the reader can no longer locate.
/// Whole-file disabling is the caller's concern (it answers with a
/// <c>disabled</c> envelope and never projects); this projector filters at
/// rule granularity only.
/// </remarks>
internal sealed class InstructionsBodyProjector
{
    private static readonly IReadOnlySet<string> EmptyRuleIds =
        new HashSet<string>(StringComparer.Ordinal);

    private readonly IConfigSnapshotAccessor _configAccessor;
    private readonly string _instructionsDirectory;
    private readonly IInstructionsOverridesAccessor _overrideAccessor;

    /// <summary>
    /// Creates a projector that reads bundled bodies from
    /// <paramref name="instructionsDirectory"/>, prefers workspace
    /// overrides from <paramref name="overrideAccessor"/>, and filters
    /// disabled rules from <paramref name="configAccessor"/>.
    /// </summary>
    /// <param name="instructionsDirectory">Absolute path of the directory
    /// holding the bundled <c>*.instructions.md</c> bodies. Must not be
    /// <see langword="null"/>, empty, or whitespace.</param>
    /// <param name="overrideAccessor">Read seam over the workspace
    /// override inventory.</param>
    /// <param name="configAccessor">Read seam over the workspace config
    /// snapshot, the source of the disabled-rule set.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="instructionsDirectory"/> is <see langword="null"/>,
    /// empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="overrideAccessor"/> or
    /// <paramref name="configAccessor"/> is <see langword="null"/>.</exception>
    public InstructionsBodyProjector(
        string instructionsDirectory,
        IInstructionsOverridesAccessor overrideAccessor,
        IConfigSnapshotAccessor configAccessor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instructionsDirectory);
        ArgumentNullException.ThrowIfNull(overrideAccessor);
        ArgumentNullException.ThrowIfNull(configAccessor);

        _instructionsDirectory = instructionsDirectory;
        _overrideAccessor = overrideAccessor;
        _configAccessor = configAccessor;
    }

    /// <summary>
    /// Reads and projects the body of <paramref name="manifestEntry"/>:
    /// resolves override-over-bundled, parses, removes the rules disabled
    /// for this file, and slices to
    /// <paramref name="requestedSectionAnchors"/> when requested.
    /// </summary>
    /// <param name="manifestEntry">The corpus file to project, identified
    /// from the in-memory snapshot. Must not be
    /// <see langword="null"/>.</param>
    /// <param name="requestedSectionAnchors">The section anchors to slice
    /// the body down to, or <see langword="null"/>/empty to return the
    /// whole projected body.</param>
    /// <param name="cancellationToken">Cancels the body read.</param>
    /// <returns>The projected body with its resolved and unresolved
    /// section anchors.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="manifestEntry"/> is
    /// <see langword="null"/>.</exception>
    public async Task<InstructionsResponseBody> ToResponseBodyAsync(
        InstructionsFileManifestEntry manifestEntry,
        IReadOnlyList<string>? requestedSectionAnchors,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifestEntry);

        var path = ResolveBodySourcePath(manifestEntry.FileName);
        var parsed = await InstructionsFileFactory
            .FromFileAsync(path, cancellationToken)
            .ConfigureAwait(false);
        var body = parsed.Body;
        var disabledRuleIds = GetDisabledRuleIds(manifestEntry.Key);

        return CreateProjection(body, requestedSectionAnchors, disabledRuleIds);
    }

    /// <summary>
    /// Reads and projects the whole body of <paramref name="manifestEntry"/>
    /// for indexing: resolves override-over-bundled, parses, removes the
    /// rules disabled for this file, and re-parses the filtered text so the
    /// returned sections carry offsets into the projected content.
    /// </summary>
    /// <param name="manifestEntry">The corpus file to project, identified
    /// from the in-memory snapshot. Must not be
    /// <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the body read.</param>
    /// <returns>The projected body text and its offset-bearing
    /// sections.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="manifestEntry"/> is
    /// <see langword="null"/>.</exception>
    public async Task<InstructionsSearchBody> ToSearchBodyAsync(
        InstructionsFileManifestEntry manifestEntry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifestEntry);

        var path = ResolveBodySourcePath(manifestEntry.FileName);
        var parsed = await InstructionsFileFactory
            .FromFileAsync(path, cancellationToken)
            .ConfigureAwait(false);
        var disabledRuleIds = GetDisabledRuleIds(manifestEntry.Key);
        var projected = parsed.Body.WithoutTaggedRules(disabledRuleIds, cancellationToken);

        return new InstructionsSearchBody(projected.RawValue, projected.Sections);
    }

    private static bool AnySectionCovers(
        IReadOnlyList<InstructionsFileTextSpan> sections,
        int offset,
        int bodyLength)
    {
        foreach (var section in sections)
        {
            if (offset >= section.StartIndex
                && (offset < section.EndIndex || (offset == bodyLength && section.EndIndex == bodyLength)))
            {
                return true;
            }
        }

        return false;
    }

    private static InstructionsResponseBody CreateProjection(
        InstructionsFileBody body,
        IReadOnlyList<string>? requestedSectionAnchors,
        IReadOnlySet<string> disabledRuleIds)
    {
        var selection = SelectSections(body.Sections, requestedSectionAnchors);
        var excludedLineSpans = GetDisabledRuleLineSpans(body.Rules, disabledRuleIds);

        var content = FilterBodyLines(body.RawValue, selection.IncludedSections, excludedLineSpans);

        return new InstructionsResponseBody(
            content,
            selection.ReturnedSections,
            selection.NotFoundSections);
    }

    private static string FilterBodyLines(
        string rawBody,
        IReadOnlyList<InstructionsFileTextSpan>? includedSections,
        IReadOnlyList<InstructionsFileLineSpan> excludedLineSpans)
    {
        ReadOnlySpan<char> body = rawBody;
        var builder = new StringBuilder(rawBody.Length);
        var lineStart = 0;
        var lineIndex = 0;
        var wroteLine = false;

        while (true)
        {
            var newlineOffset = body[lineStart..].IndexOf('\n');
            var lineEnd = newlineOffset < 0 ? body.Length : lineStart + newlineOffset;

            if (ShouldIncludeLine(lineStart, lineIndex, body.Length, includedSections, excludedLineSpans))
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

    private static List<InstructionsFileLineSpan> GetDisabledRuleLineSpans(
        IReadOnlyList<InstructionsFileRule> rules,
        IReadOnlySet<string> disabledRuleIds)
    {
        if (disabledRuleIds.Count == 0)
        {
            return [];
        }

        var spans = new List<InstructionsFileLineSpan>();

        foreach (var rule in rules)
        {
            if (rule.Id is { } id && disabledRuleIds.Contains(id))
            {
                spans.Add(rule.LineSpan);
            }
        }

        return spans;
    }

    private static SectionSelection SelectSections(
        IReadOnlyList<InstructionsFileSection> sections,
        IReadOnlyList<string>? requestedSectionAnchors)
    {
        if (requestedSectionAnchors is not { Count: > 0 })
        {
            return new SectionSelection(
                IncludedSections: null,
                ReturnedSections: [.. sections.Select(section => section.Anchor)],
                NotFoundSections: []);
        }

        var requested = new HashSet<string>(requestedSectionAnchors, StringComparer.Ordinal);
        var resolved = new HashSet<string>(StringComparer.Ordinal);
        var returned = new List<string>();
        var ranges = new List<InstructionsFileTextSpan>();

        foreach (var section in sections)
        {
            if (!requested.Contains(section.Anchor) || !resolved.Add(section.Anchor))
            {
                continue;
            }

            returned.Add(section.Anchor);
            ranges.Add(section.TextSpan);
        }

        return new SectionSelection(
            ranges,
            returned,
            [.. requestedSectionAnchors
                .Where(anchor => !resolved.Contains(anchor))
                .Distinct(StringComparer.Ordinal)]);
    }

    private static bool ShouldIncludeLine(
        int lineStart,
        int lineIndex,
        int bodyLength,
        IReadOnlyList<InstructionsFileTextSpan>? includedSections,
        IReadOnlyList<InstructionsFileLineSpan> excludedLineSpans)
    {
        if (includedSections is not null && !AnySectionCovers(includedSections, lineStart, bodyLength))
        {
            return false;
        }

        foreach (var span in excludedLineSpans)
        {
            if (lineIndex >= span.StartLine && lineIndex < span.EndLine)
            {
                return false;
            }
        }

        return true;
    }

    private IReadOnlySet<string> GetDisabledRuleIds(string key)
    {
        var entry = Array.Find(
            _configAccessor.Current.Instructions,
            file => string.Equals(file.Name, key, StringComparison.Ordinal));

        if (entry is null)
        {
            return EmptyRuleIds;
        }

        var disabled = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rule in entry.Rules)
        {
            if (rule is { Disabled: true, Id: { } id })
            {
                disabled.Add(id);
            }
        }

        return disabled;
    }

    private string ResolveBodySourcePath(string fileName)
        => _overrideAccessor.Current.TryGetPath(fileName, out var overridePath) && overridePath is not null
            ? overridePath
            : Path.Combine(_instructionsDirectory, fileName);

    /// <summary>
    /// The outcome of resolving requested section anchors against the
    /// parsed body: the text spans to keep (or <see langword="null"/> when
    /// no slicing was requested), the anchors that resolved, and the
    /// requested anchors that did not.
    /// </summary>
    /// <param name="IncludedSections">The text spans to slice the body to,
    /// or <see langword="null"/> to keep the whole body.</param>
    /// <param name="ReturnedSections">The anchors that resolved to a
    /// section, in document order.</param>
    /// <param name="NotFoundSections">The requested anchors that did not
    /// resolve to any section.</param>
    private sealed record SectionSelection(
        IReadOnlyList<InstructionsFileTextSpan>? IncludedSections,
        IReadOnlyList<string> ReturnedSections,
        IReadOnlyList<string> NotFoundSections);
}
