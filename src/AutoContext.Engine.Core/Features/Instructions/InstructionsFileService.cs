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
/// <c>disabled</c> envelope and never projects); this service filters at
/// rule granularity only.
/// </remarks>
internal sealed class InstructionsFileService
{
    private static readonly IReadOnlySet<string> EmptyRuleIds =
        new HashSet<string>(StringComparer.Ordinal);

    private readonly IConfigSnapshotAccessor _configAccessor;
    private readonly string _instructionsDirectory;
    private readonly IInstructionsOverridesAccessor _overrideAccessor;

    /// <summary>
    /// Creates a service that reads bundled bodies from
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
    public InstructionsFileService(
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
    /// Reads and projects the body of <paramref name="file"/>: resolves
    /// override-over-bundled, parses, removes the rules disabled for this
    /// file, and slices to <paramref name="sections"/> when requested.
    /// </summary>
    /// <param name="file">The corpus file to project, identified from the
    /// in-memory snapshot. Must not be <see langword="null"/>.</param>
    /// <param name="sections">The section anchors to slice the body down
    /// to, or <see langword="null"/>/empty to return the whole projected
    /// body.</param>
    /// <param name="cancellationToken">Cancels the body read.</param>
    /// <returns>The projected body with its resolved and unresolved
    /// section anchors.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="file"/> is
    /// <see langword="null"/>.</exception>
    public async Task<InstructionsBodyProjection> ProjectAsync(
        InstructionsManifestFile file,
        IReadOnlyList<string>? sections,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);

        var path = ResolveBodyPath(file.FileName);
        var parsed = await InstructionsFileFactory
            .FromFileAsync(path, cancellationToken)
            .ConfigureAwait(false);
        var body = parsed.Body;
        var disabledRuleIds = ResolveDisabledRuleIds(file.Key);

        return Project(body, sections, disabledRuleIds);
    }

    private static bool Covers(IReadOnlyList<InstructionsFileTextSpan> sections, int offset, int bodyLength)
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

    private static InstructionsBodyProjection Project(
        InstructionsFileBody body,
        IReadOnlyList<string>? requestedSections,
        IReadOnlySet<string> disabledRuleIds)
    {
        var keptSections = ResolveKeptSections(body.Sections, requestedSections, out var returned, out var notFound);
        var disabledRanges = ResolveDisabledRuleRanges(body.Rules, disabledRuleIds);

        var content = ProjectBody(body.RawValue, keptSections, disabledRanges);

        return new InstructionsBodyProjection(content, returned, notFound);
    }

    private static string ProjectBody(
        string rawBody,
        IReadOnlyList<InstructionsFileTextSpan>? keptSections,
        IReadOnlyList<InstructionsFileLineSpan> disabledRanges)
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

            if (ShouldKeepLine(lineStart, lineIndex, body.Length, keptSections, disabledRanges))
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

    private static List<InstructionsFileLineSpan> ResolveDisabledRuleRanges(
        IReadOnlyList<InstructionsFileRule> rules,
        IReadOnlySet<string> disabledRuleIds)
    {
        if (disabledRuleIds.Count == 0)
        {
            return [];
        }

        var ranges = new List<InstructionsFileLineSpan>();

        foreach (var rule in rules)
        {
            if (rule.Id is { } id && disabledRuleIds.Contains(id))
            {
                ranges.Add(rule.LineSpan);
            }
        }

        return ranges;
    }

    private static List<InstructionsFileTextSpan>? ResolveKeptSections(
        IReadOnlyList<InstructionsFileSection> sections,
        IReadOnlyList<string>? requestedSections,
        out IReadOnlyList<string> returnedSections,
        out IReadOnlyList<string> notFoundSections)
    {
        if (requestedSections is not { Count: > 0 })
        {
            returnedSections = [.. sections.Select(section => section.Anchor)];
            notFoundSections = [];
            return null;
        }

        var requested = new HashSet<string>(requestedSections, StringComparer.Ordinal);
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

        returnedSections = returned;
        notFoundSections = [.. requestedSections
            .Where(anchor => !resolved.Contains(anchor))
            .Distinct(StringComparer.Ordinal)];
        return ranges;
    }

    private static bool ShouldKeepLine(
        int lineStart,
        int lineIndex,
        int bodyLength,
        IReadOnlyList<InstructionsFileTextSpan>? keptSections,
        IReadOnlyList<InstructionsFileLineSpan> disabledRanges)
    {
        if (keptSections is not null && !Covers(keptSections, lineStart, bodyLength))
        {
            return false;
        }

        foreach (var range in disabledRanges)
        {
            if (lineIndex >= range.StartLine && lineIndex < range.EndLine)
            {
                return false;
            }
        }

        return true;
    }

    private string ResolveBodyPath(string fileName)
        => _overrideAccessor.Current.TryGetPath(fileName, out var overridePath) && overridePath is not null
            ? overridePath
            : Path.Combine(_instructionsDirectory, fileName);

    private IReadOnlySet<string> ResolveDisabledRuleIds(string key)
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
}
