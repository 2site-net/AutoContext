namespace AutoContext.Engine.Core.Machine.Housekeeping;

using AutoContext.Engine.Core.Infrastructure.Storage;
using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.Messages.Registry;

using Microsoft.Extensions.Logging;

/// <summary>
/// Walks the engine cache root once and produces a
/// <see cref="SubtreeRegistryStatus"/> for every child directory,
/// composing <see cref="RegistryEntryReader"/>'s liveness-aware
/// view of <c>engine-registry.json</c> with a structural check of
/// each on-disk subtree's name. Pure: no deletion, no policy
/// decisions, no I/O beyond the directory enumeration and the
/// registry read. <c>StaleSubtreeCleaner</c> and
/// <c>HousekeepingService</c> consume the result downstream.
/// </summary>
/// <remarks>
/// <para>
/// The canonical on-disk shape is
/// <c>&lt;cacheRoot&gt;/&lt;workspaceHash&gt;/&lt;instanceId&gt;</c>:
/// a 16-uppercase-hex parent directory whose children are
/// <see cref="Guid"/>-named per-instance subtrees. The scanner
/// classifies each <c>&lt;workspaceHash&gt;/&lt;instanceId&gt;</c>
/// against the registry into one of four arms:
/// </para>
/// <list type="number">
///   <item><see cref="SubtreeRegistryStatus.Registered"/> — backed
///   by a live registry entry (pid alive, start time matches).</item>
///   <item><see cref="SubtreeRegistryStatus.StaleRegistration"/> —
///   backed by a stale registry entry (pid gone or recycled).</item>
///   <item><see cref="SubtreeRegistryStatus.Unregistered"/> —
///   canonical shape but no registry entry claims it.</item>
///   <item><see cref="SubtreeRegistryStatus.Foreign"/> — any other
///   shape (legacy flat
///   <c>&lt;workspaceHash&gt;#&lt;instanceId&gt;</c>, bare
///   <c>&lt;workspaceHash&gt;</c>, non-hex top-level dirs, or
///   workspace-hash dirs holding non-<see cref="Guid"/>
///   children).</item>
/// </list>
/// <para>
/// Stateless and side-effect-free; safe to register as a singleton
/// and call from concurrent housekeeping passes. The scanner does
/// not create the cache-root directory — if it doesn't exist on
/// disk, the scan returns an empty list (there is nothing to
/// classify).
/// </para>
/// </remarks>
internal sealed partial class CacheRootScanner
{
    private readonly CacheRoot _cacheRoot;
    private readonly RegistryEntryReader _entryReader;
    private readonly ILogger<CacheRootScanner> _logger;

    /// <summary>
    /// Creates a new scanner rooted at <paramref name="cacheRoot"/>
    /// and composing over <paramref name="entryReader"/>. The scan
    /// walks <see cref="CacheRoot.FullPath"/>; this engine's own
    /// per-instance subtree is one of the directories the scan
    /// classifies, no different from any peer.
    /// </summary>
    /// <param name="cacheRoot">Resolved cache-root identity bundle
    /// the scan walks.</param>
    /// <param name="entryReader">Liveness-aware registry reader.</param>
    /// <param name="logger">Diagnostic sink.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="cacheRoot"/>, <paramref name="entryReader"/>,
    /// or <paramref name="logger"/> is <see langword="null"/>.</exception>
    public CacheRootScanner(
        CacheRoot cacheRoot,
        RegistryEntryReader entryReader,
        ILogger<CacheRootScanner> logger)
    {
        ArgumentNullException.ThrowIfNull(cacheRoot);
        ArgumentNullException.ThrowIfNull(entryReader);
        ArgumentNullException.ThrowIfNull(logger);

        _cacheRoot = cacheRoot;
        _entryReader = entryReader;
        _logger = logger;
    }

    /// <summary>
    /// Walks the cache root once and classifies every child
    /// directory against the current registry snapshot. A missing
    /// cache-root directory yields an empty list.
    /// </summary>
    /// <param name="cancellationToken">Cancels the walk.</param>
    /// <returns>Frozen list of one <see cref="SubtreeRegistryStatus"/>
    /// per discovered directory (one entry per canonical
    /// per-instance subtree, one entry per foreign top-level
    /// directory, one entry per non-<see cref="Guid"/> child of a
    /// workspace-hash parent).</returns>
    /// <exception cref="IOException">The underlying
    /// <see cref="RegistryEntryReader"/> exhausted its retry budget
    /// without acquiring a shared read handle.</exception>
    public async Task<IReadOnlyList<SubtreeRegistryStatus>> ScanAsync(
        CancellationToken cancellationToken = default)
    {
        var cacheRootPath = _cacheRoot.FullPath;

        if (!Directory.Exists(cacheRootPath))
        {
            return [];
        }

        var probeResults = await _entryReader
            .ReadAsync(cancellationToken)
            .ConfigureAwait(false);
        var entryIndex = BuildEntryIndex(probeResults);

        var results = new List<SubtreeRegistryStatus>();

        foreach (var topLevelDir in Directory.EnumerateDirectories(cacheRootPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ClassifyTopLevelDirectory(topLevelDir, entryIndex, results);
        }

        return results;
    }

    private static Dictionary<(string WorkspaceHash, Guid InstanceId), RegistryEntryProbeResult> BuildEntryIndex(
        IReadOnlyList<RegistryEntryProbeResult> probeResults)
    {
        var index = new Dictionary<(string, Guid), RegistryEntryProbeResult>(probeResults.Count);

        foreach (var probe in probeResults)
        {
            // First write wins on the (unlikely) duplicate-key
            // case — the registry is append-only with a fresh
            // instanceId per spawn, so a collision indicates a
            // launcher bug we already reject elsewhere.
            index.TryAdd((probe.Entry.WorkspaceHash, probe.Entry.InstanceId), probe);
        }

        return index;
    }

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Cache-root subtree {SubtreePath} classified as Foreign: workspace-hash directory holds no per-instance subdirectories.")]
    private static partial void LogForeignBareWorkspaceHash(ILogger logger, string subtreePath);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Debug,
        Message = "Cache-root subtree {SubtreePath} classified as Foreign: child of workspace-hash directory is not a Guid.")]
    private static partial void LogForeignNonGuidInstance(ILogger logger, string subtreePath);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Cache-root subtree {SubtreePath} classified as Foreign: top-level directory name is not a workspace hash.")]
    private static partial void LogForeignTopLevel(ILogger logger, string subtreePath);

    private SubtreeRegistryStatus ClassifyInstanceDirectory(
        string instanceDir,
        string workspaceHashValue,
        IReadOnlyDictionary<(string WorkspaceHash, Guid InstanceId), RegistryEntryProbeResult> entryIndex)
    {
        var instanceName = Path.GetFileName(instanceDir);

        if (!Guid.TryParseExact(instanceName, "D", out var instanceId))
        {
            LogForeignNonGuidInstance(_logger, instanceDir);
            return new SubtreeRegistryStatus.Foreign(instanceDir);
        }

        if (!entryIndex.TryGetValue((workspaceHashValue, instanceId), out var probe))
        {
            return new SubtreeRegistryStatus.Unregistered(instanceDir);
        }

        return probe.State == RegistryEntryProbeState.Live
            ? new SubtreeRegistryStatus.Registered(instanceDir, probe.Entry)
            : new SubtreeRegistryStatus.StaleRegistration(instanceDir, probe.Entry);
    }

    private void ClassifyTopLevelDirectory(
        string topLevelDir,
        IReadOnlyDictionary<(string WorkspaceHash, Guid InstanceId), RegistryEntryProbeResult> entryIndex,
        List<SubtreeRegistryStatus> results)
    {
        var name = Path.GetFileName(topLevelDir);

        if (!WorkspaceHash.TryParse(name, provider: null, out var workspaceHash))
        {
            // Legacy flat <workspaceHash>#<instanceId>, bare
            // non-hex names, or anything else that fails the
            // 16-uppercase-hex shape.
            LogForeignTopLevel(_logger, topLevelDir);
            results.Add(new SubtreeRegistryStatus.Foreign(topLevelDir));
            return;
        }

        var instanceDirs = Directory.EnumerateDirectories(topLevelDir).ToArray();

        if (instanceDirs.Length == 0)
        {
            // Bare <workspaceHash> directory with no per-instance
            // children — pre-nested-layout leftover.
            LogForeignBareWorkspaceHash(_logger, topLevelDir);
            results.Add(new SubtreeRegistryStatus.Foreign(topLevelDir));
            return;
        }

        foreach (var instanceDir in instanceDirs)
        {
            results.Add(ClassifyInstanceDirectory(instanceDir, workspaceHash.Value, entryIndex));
        }
    }
}
