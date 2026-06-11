namespace AutoContext.Engine.Core.Features.Instructions;

using AutoContext.Engine.Core.Features.Instructions.Snapshot;
using AutoContext.Engine.Core.Workspace.Config;

using AutoContext.Instructions.Parser.Model;

using Microsoft.Extensions.Logging;

/// <summary>
/// Free-text body search across the bundled instructions corpus. Indexes
/// each file's <see cref="InstructionsFileManifestEntry.Description"/> and its
/// projected body (via <see cref="InstructionsBodyProjector.ToSearchBodyAsync"/>,
/// so workspace overrides and disabled-rule filtering are already
/// reconciled) into per-file token-frequency maps with an identifier-aware
/// tokenizer that splits on non-word runs <em>and</em> camelCase / kebab /
/// snake boundaries, so a query of <c>ConfigureAwait</c> matches a heading
/// written as "Configure Await".
/// </summary>
/// <remarks>
/// <para>
/// <b>Match:</b> AND across distinct query tokens — a file matches only when
/// every tokenized query piece appears in either map.
/// </para>
/// <para>
/// <b>Score:</b> per matching query token, <c>descHits * 2 + bodyHits</c>,
/// summed across the distinct query tokens. Ties break by
/// <see cref="InstructionsSearchBodyHit.Key"/> ascending for deterministic
/// output.
/// </para>
/// <para>
/// <b>Excerpts:</b> up to three body windows per hit, ordered by earliest
/// position, each snapped to whitespace and carrying the anchor of the
/// section it falls in.
/// </para>
/// <para>
/// <b>Index lifecycle:</b> built lazily on the first <see cref="SearchAsync"/>
/// call (projector reads are async, so an eager build would force callers to
/// await it). It is rebuilt whole on the next search after
/// <see cref="Invalidate"/> — a full rebuild of the small corpus is cheap, so
/// there is no per-file invalidation. Whole-file disabled state is captured
/// at index time and honored per query through the <c>includeDisabled</c>
/// flag, rather than dropping disabled files from the index.
/// </para>
/// </remarks>
internal sealed partial class InstructionsFullTextSearchService : IDisposable
{
    private const int DefaultLimit = 10;
    private const int ExcerptRadius = 80;
    private const int MaxExcerptsPerHit = 3;
    private const int MaxLimit = 25;
    private readonly InstructionsBodyProjector _bodyProjector;
    private readonly IConfigSnapshotAccessor _configAccessor;
    private volatile bool _dirty;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<IndexedFile>? _index;
    private readonly ILogger<InstructionsFullTextSearchService> _logger;
    private readonly IInstructionsManifestAccessor _manifestAccessor;

    /// <summary>
    /// Creates a search service over the corpus snapshot from
    /// <paramref name="manifestAccessor"/>, projecting bodies through
    /// <paramref name="bodyProjector"/> and reading whole-file disabled state
    /// from <paramref name="configAccessor"/>.
    /// </summary>
    /// <param name="manifestAccessor">Read seam over the in-memory corpus
    /// snapshot.</param>
    /// <param name="bodyProjector">Projects one file body per index entry.</param>
    /// <param name="configAccessor">Read seam over the workspace config, the
    /// source of whole-file disabled state.</param>
    /// <param name="logger">Diagnostic sink.</param>
    /// <exception cref="ArgumentNullException">Any argument is
    /// <see langword="null"/>.</exception>
    public InstructionsFullTextSearchService(
        IInstructionsManifestAccessor manifestAccessor,
        InstructionsBodyProjector bodyProjector,
        IConfigSnapshotAccessor configAccessor,
        ILogger<InstructionsFullTextSearchService> logger)
    {
        ArgumentNullException.ThrowIfNull(manifestAccessor);
        ArgumentNullException.ThrowIfNull(bodyProjector);
        ArgumentNullException.ThrowIfNull(configAccessor);
        ArgumentNullException.ThrowIfNull(logger);

        _manifestAccessor = manifestAccessor;
        _bodyProjector = bodyProjector;
        _configAccessor = configAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public void Dispose()
        => _gate.Dispose();

    /// <summary>
    /// Marks the index stale so the next <see cref="SearchAsync"/> rebuilds it.
    /// Wired to the override-inventory and config change signals.
    /// </summary>
    public void Invalidate()
        => _dirty = true;

    /// <summary>
    /// Searches the corpus for files matching every distinct token of
    /// <paramref name="query"/>, ranked by relevance.
    /// </summary>
    /// <param name="query">The search query. When it tokenizes to nothing the
    /// result is empty. Must not be <see langword="null"/>.</param>
    /// <param name="limit">The maximum number of hits to return; clamped to
    /// <c>[1, 25]</c>, defaulting to 10 when <see langword="null"/> or not
    /// positive.</param>
    /// <param name="includeDisabled">When <see langword="false"/>, files the
    /// workspace has disabled whole are excluded.</param>
    /// <param name="cancellationToken">Cancels the index build.</param>
    /// <returns>The ranked hits, highest score first, ties broken by
    /// <see cref="InstructionsSearchBodyHit.Key"/> ascending.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is
    /// <see langword="null"/>.</exception>
    public async Task<IReadOnlyList<InstructionsSearchBodyHit>> SearchAsync(
        string query,
        int? limit,
        bool includeDisabled,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var queryTokens = InstructionsSearchTokenizer.CollectQueryTokens(query);

        if (queryTokens.Count == 0)
        {
            return [];
        }

        var effectiveLimit = NormalizeLimit(limit);
        var index = await GetIndexAsync(cancellationToken).ConfigureAwait(false);
        var hits = new List<InstructionsSearchBodyHit>();

        foreach (var file in index)
        {
            if (file.Disabled && !includeDisabled)
            {
                continue;
            }

            var score = ScoreFile(file, queryTokens);

            if (score <= 0)
            {
                continue;
            }

            hits.Add(new InstructionsSearchBodyHit(
                file.Key,
                file.FileName,
                file.Name,
                file.Description,
                score,
                ExtractExcerpts(file, queryTokens)));
        }

        hits.Sort(static (left, right) =>
        {
            var byScore = right.Score.CompareTo(left.Score);

            return byScore != 0 ? byScore : string.CompareOrdinal(left.Key, right.Key);
        });

        return hits.Count <= effectiveLimit ? hits : hits.GetRange(0, effectiveLimit);
    }

    private static List<InstructionsSearchBodyExcerpt> ExtractExcerpts(
        IndexedFile file,
        IReadOnlyList<string> queryTokens)
    {
        var positions = FindMatchPositions(file.Body, queryTokens);

        if (positions.Count == 0)
        {
            return [];
        }

        var excerpts = new List<InstructionsSearchBodyExcerpt>();
        var lastEnd = -1;

        foreach (var position in positions)
        {
            if (excerpts.Count >= MaxExcerptsPerHit)
            {
                break;
            }

            var window = SliceWindow(file.Body, position.Start, position.End);

            if (window.Start <= lastEnd)
            {
                continue;
            }

            var section = FindSectionForOffset(file.Sections, position.Start);
            var snippet = file.Body.AsSpan(window.Start, window.End - window.Start).Trim();

            excerpts.Add(new InstructionsSearchBodyExcerpt(
                section?.Anchor ?? string.Empty,
                snippet.ToString(),
                LineForOffset(file.Body, position.Start)));
            lastEnd = window.End;
        }

        return excerpts;
    }

    private static List<MatchPosition> FindMatchPositions(
        string body,
        IReadOnlyList<string> queryTokens)
    {
        var bodySpan = body.AsSpan();
        var seen = new HashSet<int>();
        var positions = new List<MatchPosition>();

        foreach (var token in queryTokens)
        {
            if (token.Length == 0)
            {
                continue;
            }

            var from = 0;
            var found = 0;

            while (found < MaxExcerptsPerHit)
            {
                var relative = bodySpan[from..].IndexOf(token, StringComparison.OrdinalIgnoreCase);

                if (relative < 0)
                {
                    break;
                }

                var index = from + relative;

                if (seen.Add(index))
                {
                    positions.Add(new MatchPosition(index, index + token.Length));
                }

                from = index + token.Length;
                found++;
            }
        }

        positions.Sort(static (left, right) => left.Start.CompareTo(right.Start));

        return positions;
    }

    private static InstructionsFileSection? FindSectionForOffset(
        IReadOnlyList<InstructionsFileSection> sections,
        int offset)
    {
        var low = 0;
        var high = sections.Count - 1;

        while (low <= high)
        {
            var mid = (low + high) / 2;
            var span = sections[mid].TextSpan;

            if (offset < span.StartIndex)
            {
                high = mid - 1;
                continue;
            }

            if (offset >= span.EndIndex)
            {
                low = mid + 1;
                continue;
            }

            return sections[mid];
        }

        return null;
    }

    private static int LineForOffset(string body, int offset)
    {
        var limit = Math.Min(offset, body.Length);

        return 1 + body.AsSpan(0, limit).Count('\n');
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Content-search index: failed to project '{FileName}'; skipping.")]
    private static partial void LogProjectionFailed(ILogger logger, string fileName, Exception exception);

    private static int NormalizeLimit(int? input)
        => input is { } value && value > 0 ? Math.Min(value, MaxLimit) : DefaultLimit;

    private static int ScoreFile(IndexedFile file, IReadOnlyList<string> queryTokens)
    {
        var score = 0;

        foreach (var token in queryTokens)
        {
            var descriptionHits = file.DescriptionTokens.GetValueOrDefault(token);
            var bodyHits = file.BodyTokens.GetValueOrDefault(token);

            if (descriptionHits == 0 && bodyHits == 0)
            {
                return 0;
            }

            score += (descriptionHits * 2) + bodyHits;
        }

        return score;
    }

    private static TextWindow SliceWindow(string body, int start, int end)
    {
        var windowStart = Math.Max(0, start - ExcerptRadius);
        var windowEnd = Math.Min(body.Length, end + ExcerptRadius);

        while (windowStart > 0
            && !char.IsWhiteSpace(body[windowStart - 1])
            && start - windowStart < ExcerptRadius * 2)
        {
            windowStart--;
        }

        while (windowEnd < body.Length
            && !char.IsWhiteSpace(body[windowEnd])
            && windowEnd - end < ExcerptRadius * 2)
        {
            windowEnd++;
        }

        return new TextWindow(windowStart, windowEnd);
    }

    private async Task<IndexedFile?> BuildFileIndexAsync(
        InstructionsFileManifestEntry file,
        CancellationToken cancellationToken)
    {
        InstructionsSearchBody projected;

        try
        {
            projected = await _bodyProjector.ToSearchBodyAsync(file, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogProjectionFailed(_logger, file.FileName, exception);

            return null;
        }

        return new IndexedFile(
            file.Key,
            file.FileName,
            file.Name,
            file.Description,
            projected.Content,
            projected.Sections,
            InstructionsSearchTokenizer.Tokenize(file.Description),
            InstructionsSearchTokenizer.Tokenize(projected.Content),
            IsDisabled(file.Key));
    }

    private async Task<IReadOnlyList<IndexedFile>> BuildIndexAsync(CancellationToken cancellationToken)
    {
        var files = _manifestAccessor.Current.Files;
        var indexed = new List<IndexedFile>(files.Count);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entry = await BuildFileIndexAsync(file, cancellationToken).ConfigureAwait(false);

            if (entry is not null)
            {
                indexed.Add(entry);
            }
        }

        return indexed;
    }

    private async Task<IReadOnlyList<IndexedFile>> GetIndexAsync(CancellationToken cancellationToken)
    {
        if (_index is { } ready && !_dirty)
        {
            return ready;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_index is { } cached && !_dirty)
            {
                return cached;
            }

            var built = await BuildIndexAsync(cancellationToken).ConfigureAwait(false);
            _index = built;
            _dirty = false;

            return built;
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool IsDisabled(string key)
    {
        var entry = Array.Find(
            _configAccessor.Current.Instructions,
            file => string.Equals(file.Name, key, StringComparison.Ordinal));

        return entry?.Disabled == true;
    }

    private sealed record IndexedFile(
        string Key,
        string FileName,
        string Name,
        string Description,
        string Body,
        IReadOnlyList<InstructionsFileSection> Sections,
        IReadOnlyDictionary<string, int> DescriptionTokens,
        IReadOnlyDictionary<string, int> BodyTokens,
        bool Disabled);

    private readonly record struct MatchPosition(int Start, int End);

    private readonly record struct TextWindow(int Start, int End);
}
