namespace AutoContext.Engine.Core.Features.Instructions;

using AutoContext.Engine.Core.Features.Instructions.Snapshot;
using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;

using AutoContext.Instructions.Parser;
using AutoContext.Instructions.Parser.Model;

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
            .ParseFileAsync(path, cancellationToken)
            .ConfigureAwait(false);
        var body = parsed.Body;
        var disabledRuleIds = ResolveDisabledRuleIds(file.Key);

        return Project(body, sections, disabledRuleIds);
    }

    private static Dictionary<int, int> BuildLineForOffset(string[] lines)
    {
        var lineForOffset = new Dictionary<int, int>(lines.Length);
        var offset = 0;

        for (var index = 0; index < lines.Length; index++)
        {
            lineForOffset[offset] = index;
            offset += lines[index].Length + 1;
        }

        return lineForOffset;
    }

    private static bool[] BuildSectionMask(
        InstructionsFileBody body,
        string[] lines,
        IReadOnlyList<string>? requestedSections,
        out IReadOnlyList<string> returnedSections,
        out IReadOnlyList<string> notFoundSections)
    {
        var keep = new bool[lines.Length];

        if (requestedSections is not { Count: > 0 })
        {
            Array.Fill(keep, true);
            returnedSections = [.. body.Sections.Select(section => section.Anchor)];
            notFoundSections = [];
            return keep;
        }

        var lineForOffset = BuildLineForOffset(lines);
        var requested = new HashSet<string>(requestedSections, StringComparer.Ordinal);
        var resolved = new HashSet<string>(StringComparer.Ordinal);
        var returned = new List<string>();

        foreach (var section in body.Sections)
        {
            if (!requested.Contains(section.Anchor) || !resolved.Add(section.Anchor))
            {
                continue;
            }

            returned.Add(section.Anchor);
            MarkSection(keep, lineForOffset, body.RawValue.Length, section);
        }

        returnedSections = returned;
        notFoundSections = [.. requestedSections
            .Where(anchor => !resolved.Contains(anchor))
            .Distinct(StringComparer.Ordinal)];
        return keep;
    }

    private static void FilterDisabledRules(
        bool[] keep,
        IReadOnlyList<InstructionsFileRule> rules,
        IReadOnlySet<string> disabledRuleIds)
    {
        if (disabledRuleIds.Count == 0)
        {
            return;
        }

        foreach (var rule in rules)
        {
            if (rule.Id is not { } id || !disabledRuleIds.Contains(id))
            {
                continue;
            }

            for (var index = rule.LineSpan.StartLine; index < rule.LineSpan.EndLine; index++)
            {
                keep[index] = false;
            }
        }
    }

    private static void MarkSection(
        bool[] keep,
        Dictionary<int, int> lineForOffset,
        int bodyLength,
        InstructionsFileSection section)
    {
        var startLine = lineForOffset[section.TextSpan.StartIndex];
        var endLineExclusive = section.TextSpan.EndIndex >= bodyLength || !lineForOffset.TryGetValue(section.TextSpan.EndIndex, out var end)
            ? keep.Length
            : end;

        for (var index = startLine; index < endLineExclusive; index++)
        {
            keep[index] = true;
        }
    }

    private static InstructionsBodyProjection Project(
        InstructionsFileBody body,
        IReadOnlyList<string>? requestedSections,
        IReadOnlySet<string> disabledRuleIds)
    {
        var lines = body.RawValue.Split('\n');
        var keep = BuildSectionMask(body, lines, requestedSections, out var returned, out var notFound);

        FilterDisabledRules(keep, body.Rules, disabledRuleIds);

        var content = string.Join('\n', lines.Where((_, index) => keep[index]));

        return new InstructionsBodyProjection(content, returned, notFound);
    }

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

    private string ResolveBodyPath(string fileName)
        => _overrideAccessor.Current.TryGetPath(fileName, out var overridePath) && overridePath is not null
            ? overridePath
            : Path.Combine(_instructionsDirectory, fileName);
}
