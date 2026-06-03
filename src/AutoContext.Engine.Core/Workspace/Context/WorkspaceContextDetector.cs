namespace AutoContext.Engine.Core.Workspace.Context;

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

using AutoContext.Engine.Core.Infrastructure.Events;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Detects the technologies present in a workspace by classifying its
/// files against the declarative <see cref="WorkspaceDetectionRules"/>
/// tables, then propagating implied flags through the activation
/// cascade. A single recursive traversal (<see cref="DetectAsync"/>)
/// seeds an inverted contribution index — every file records the base
/// flags it raises, and every base flag keeps a live contributor count —
/// so later filesystem events reclassify exactly one path and adjust the
/// counts in place rather than re-scanning the workspace.
/// </summary>
/// <remarks>
/// <para>
/// The detector is a faithful port of the VS Code extension's
/// <c>workspace-context-detector.ts</c>, with two deliberate changes
/// dictated by the engine's lack of an indexed <c>findFiles</c>. First,
/// the ~40 per-rule globs the extension issues are replaced by one
/// classification index (<see cref="_extensionToFlags"/>,
/// <see cref="_fileNameToFlags"/>, and the small glob list
/// <see cref="_globRules"/>) consulted during a single workspace walk.
/// Second, incremental updates use the count-based inverted index
/// (<see cref="_contributions"/> / <see cref="_baseCounts"/>) instead of
/// re-globbing on delete: dropping the last contributor for a flag flips
/// it off, while a surviving sibling keeps it on.
/// </para>
/// <para>
/// The recursive <see cref="FileSystemWatcher"/> armed by
/// <see cref="Watch"/> filters events in-code through the same selector
/// dictionaries that drive classification, so the watch surface can
/// never drift from the detection rules. Synthetic flags
/// (<c>hasGit</c>, <c>hasNodeJs</c>) and the cascade-to-fixpoint
/// semantics match the TypeScript source exactly. Detection passes and
/// watcher-driven reclassifications are serialised through an internal
/// gate; reads of <see cref="Current"/> are lock-free.
/// </para>
/// </remarks>
internal sealed partial class WorkspaceContextDetector : IDisposable
{
    private const string HasGitFlag = "hasGit";
    private const string HasNodeJsFlag = "hasNodeJs";
    private const string PackageJsonFileName = "package.json";

    private static readonly TimeSpan DefaultDebounceDelay = TimeSpan.FromMilliseconds(500);

    private static readonly FrozenSet<string> ExcludedDirectories =
        new[] { "node_modules", "bin", "obj", ".git" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> ExcludedDirectoriesLookup =
        ExcludedDirectories.GetAlternateLookup<ReadOnlySpan<char>>();

    private static readonly EnumerationOptions WalkOptions = new()
    {
        IgnoreInaccessible = true,
        AttributesToSkip = 0,
        RecurseSubdirectories = false,
    };

    private readonly Dictionary<string, int> _baseCounts = new(StringComparer.Ordinal);
    private readonly (FrozenSet<string> Extensions, FrozenSet<string> FileNames, IReadOnlyList<ContentPatternRule> Rules)[] _contentScans;
    private readonly Dictionary<string, HashSet<string>> _contributions = new(StringComparer.OrdinalIgnoreCase);
    private WorkspaceDetectionResult _current;
    private readonly TrailingEdgeDebouncer _debouncer;
    private bool _disposed;
    private readonly FrozenDictionary<string, string[]> _extensionToFlags;
    private readonly FrozenDictionary<string, string[]> _fileNameToFlags;
    private readonly IReadOnlyList<FlagActivationEdge> _flagActivationEdges;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly (Regex Pattern, string Flag)[] _globRules;
    private bool _hasGit;
    private readonly ILogger<WorkspaceContextDetector> _logger;
    private readonly Lock _pendingLock = new();
    private readonly HashSet<string> _pendingPaths = new(StringComparer.OrdinalIgnoreCase);
    private FileSystemWatcher? _watcher;
    private readonly string _workspacePath;

    /// <summary>
    /// Creates a detector bound to <paramref name="workspacePath"/> and
    /// builds its classification index from the supplied rule tables.
    /// The detector is inert until <see cref="DetectAsync"/> seeds the
    /// index; arm incremental updates with <see cref="Watch"/>.
    /// </summary>
    /// <param name="workspacePath">Absolute path of the workspace folder
    /// to scan. Must not be <see langword="null"/> or whitespace.</param>
    /// <param name="fileRules">File-presence rules whose selectors are
    /// indexed for classification.</param>
    /// <param name="contentScans">Content-scan groups whose manifest
    /// selectors and body patterns are indexed for classification.</param>
    /// <param name="activationEdges">Activation cascade edges walked
    /// after base detection.</param>
    /// <param name="logger">Diagnostic sink. <see langword="null"/>
    /// silences diagnostics.</param>
    /// <param name="timeProvider">Clock that schedules the incremental
    /// debounce window. <see langword="null"/> uses
    /// <see cref="TimeProvider.System"/>.</param>
    /// <param name="debounceDelay">Quiet window the watcher waits for
    /// after the last filesystem event before reclassifying.
    /// <see langword="null"/> uses 500&#160;ms. Must be positive when
    /// supplied.</param>
    /// <exception cref="ArgumentException"><paramref name="workspacePath"/>
    /// is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="fileRules"/>,
    /// <paramref name="contentScans"/>, or
    /// <paramref name="activationEdges"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="debounceDelay"/> is zero or negative.</exception>
    public WorkspaceContextDetector(
        string workspacePath,
        IReadOnlyList<FilePresenceRule> fileRules,
        IReadOnlyList<ContentScan> contentScans,
        IReadOnlyList<FlagActivationEdge> activationEdges,
        ILogger<WorkspaceContextDetector>? logger = null,
        TimeProvider? timeProvider = null,
        TimeSpan? debounceDelay = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentNullException.ThrowIfNull(fileRules);
        ArgumentNullException.ThrowIfNull(contentScans);
        ArgumentNullException.ThrowIfNull(activationEdges);

        if (debounceDelay is { } delay)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
                delay, TimeSpan.Zero, nameof(debounceDelay));
        }

        _workspacePath = workspacePath;
        _logger = logger ?? NullLogger<WorkspaceContextDetector>.Instance;
        _flagActivationEdges = activationEdges;

        var extensionMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var fileNameMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var globRules = new List<(Regex, string)>();

        foreach (var rule in fileRules)
        {
            foreach (var selector in rule.Selectors)
            {
                switch (selector.Kind)
                {
                    case FileSelectorKind.Extension:
                        AddFlag(extensionMap, selector.Value, rule.Flag);
                        break;
                    case FileSelectorKind.FileName:
                        AddFlag(fileNameMap, selector.Value, rule.Flag);
                        break;
                    case FileSelectorKind.GlobPattern:
                        globRules.Add((GlobToRegex(selector.Value), rule.Flag));
                        break;
                    default:
                        break;
                }
            }
        }

        _extensionToFlags = extensionMap.ToFrozenDictionary(
            static pair => pair.Key,
            static pair => pair.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);
        _fileNameToFlags = fileNameMap.ToFrozenDictionary(
            static pair => pair.Key,
            static pair => pair.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);
        _globRules = [.. globRules];

        _contentScans =
        [
            .. contentScans.Select(static scan => (
                Extensions: scan.Selectors
                    .Where(static s => s.Kind == FileSelectorKind.Extension)
                    .Select(static s => s.Value)
                    .ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                FileNames: scan.Selectors
                    .Where(static s => s.Kind == FileSelectorKind.FileName)
                    .Select(static s => s.Value)
                    .ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                scan.Rules)),
        ];

        _debouncer = new TrailingEdgeDebouncer(
            FlushAsync, timeProvider ?? TimeProvider.System, debounceDelay ?? DefaultDebounceDelay);
        _current = WorkspaceDetectionResult.Empty;
    }

    /// <summary>
    /// Raised after a watcher-driven reclassification publishes a
    /// detection result whose flag set differs from the previous one,
    /// carrying the result now in <see cref="Current"/>. Not raised by
    /// <see cref="DetectAsync"/> — the initial pass populates
    /// <see cref="Current"/> silently so the caller can prime its own
    /// consumers.
    /// </summary>
    public event EventHandler<WorkspaceDetectionResult>? Changed;

    /// <summary>
    /// The latest detection result. Each read returns an immutable value
    /// that never changes after it is published; safe to use without
    /// locking. Seeded with <see cref="WorkspaceDetectionResult.Empty"/>
    /// until the first <see cref="DetectAsync"/> completes.
    /// </summary>
    public WorkspaceDetectionResult Current
        => Volatile.Read(ref _current);

    /// <summary>
    /// Runs a full workspace scan: clears the contribution index, walks
    /// the workspace once (pruning <c>node_modules</c> / <c>bin</c> /
    /// <c>obj</c> / <c>.git</c>), classifies every file, runs the
    /// activation cascade, and publishes the result to
    /// <see cref="Current"/>. Does not raise <see cref="Changed"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <returns>The detection result now held in
    /// <see cref="Current"/>.</returns>
    public async Task<WorkspaceDetectionResult> DetectAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            _contributions.Clear();
            _baseCounts.Clear();
            _hasGit = Directory.Exists(Path.Combine(_workspacePath, ".git"));

            foreach (var fullPath in EnumerateWorkspaceFiles(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var flags = await ClassifyFileAsync(
                    fullPath, RelativePath(fullPath), cancellationToken).ConfigureAwait(false);

                if (flags.Count > 0)
                {
                    ApplyContribution(fullPath, flags);
                }
            }

            var result = BuildResult();
            Volatile.Write(ref _current, result);
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _watcher?.Dispose();
        _debouncer.Dispose();
        _gate.Dispose();
    }

    /// <summary>
    /// Arms a recursive <see cref="FileSystemWatcher"/> over the
    /// workspace so later create / change / delete / rename events
    /// reclassify the affected paths and republish
    /// <see cref="Current"/>. Idempotent; subsequent calls are no-ops
    /// while a watcher is active. Call after <see cref="DetectAsync"/>
    /// has seeded the index.
    /// </summary>
    public void Watch()
    {
        if (_watcher is not null || !Directory.Exists(_workspacePath))
        {
            return;
        }

        _debouncer.Run();

        var watcher = new FileSystemWatcher(_workspacePath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
        };

        watcher.Created += OnFileSystemEvent;
        watcher.Changed += OnFileSystemEvent;
        watcher.Deleted += OnFileSystemEvent;
        watcher.Renamed += OnRenamed;
        watcher.EnableRaisingEvents = true;

        _watcher = watcher;
    }

    private static void AddFlag(Dictionary<string, List<string>> map, string key, string flag)
    {
        if (map.TryGetValue(key, out var flags))
        {
            flags.Add(flag);
        }
        else
        {
            map[key] = [flag];
        }
    }

    private static string ExtensionOf(string fileName)
    {
        var dot = fileName.LastIndexOf('.');
        return dot >= 0 ? fileName[(dot + 1)..] : string.Empty;
    }

    private static Regex GlobToRegex(string glob)
    {
        var normalized = glob.Replace('\\', '/');
        var pattern = new StringBuilder("^");

        for (var i = 0; i < normalized.Length;)
        {
            var c = normalized[i];

            if (c == '?')
            {
                pattern.Append("[^/]");
                i++;

                continue;
            }

            if (c != '*')
            {
                pattern.Append(Regex.Escape(c.ToString()));
                i++;

                continue;
            }

            if (i + 1 >= normalized.Length || normalized[i + 1] != '*')
            {
                pattern.Append("[^/]*");
                i++;

                continue;
            }

            if (i + 2 < normalized.Length && normalized[i + 2] == '/')
            {
                pattern.Append("(?:.*/)?");
                i += 3;
            }
            else
            {
                pattern.Append(".*");
                i += 2;
            }
        }

        pattern.Append('$');
        return new Regex(
            pattern.ToString(),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }

    private static bool IsCritical(Exception ex)
        => ex is OutOfMemoryException
            or StackOverflowException
            or AccessViolationException
            or ThreadAbortException;

    private static bool IsExcluded(string relativePath)
    {
        var remaining = relativePath.AsSpan();

        while (!remaining.IsEmpty)
        {
            var slash = remaining.IndexOf('/');
            var segment = slash < 0 ? remaining : remaining[..slash];

            if (ExcludedDirectoriesLookup.Contains(segment))
            {
                return true;
            }

            if (slash < 0)
            {
                break;
            }

            remaining = remaining[(slash + 1)..];
        }

        return false;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Incremental workspace detection failed.")]
    private static partial void LogIncrementalDetectionFailed(ILogger logger, Exception exception);

    private static IEnumerable<string> SafeEnumerate(string directory, bool isDirectory)
    {
        var source = isDirectory
            ? Directory.EnumerateDirectories(directory, "*", WalkOptions)
            : Directory.EnumerateFiles(directory, "*", WalkOptions);

        using var enumerator = source.GetEnumerator();

        while (true)
        {
            try
            {
                if (!enumerator.MoveNext())
                {
                    yield break;
                }
            }
            catch (IOException)
            {
                yield break;
            }
            catch (UnauthorizedAccessException)
            {
                yield break;
            }

            yield return enumerator.Current;
        }
    }

    private static async Task<string?> TryReadAsync(string fullPath, CancellationToken cancellationToken)
    {
        try
        {
            return await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void ApplyContribution(string fullPath, HashSet<string> newFlags)
    {
        _contributions.TryGetValue(fullPath, out var oldFlags);

        if (oldFlags is not null)
        {
            foreach (var flag in oldFlags)
            {
                if (!newFlags.Contains(flag))
                {
                    Decrement(flag);
                }
            }
        }

        foreach (var flag in newFlags)
        {
            if (oldFlags is null || !oldFlags.Contains(flag))
            {
                _baseCounts[flag] = _baseCounts.GetValueOrDefault(flag) + 1;
            }
        }

        if (newFlags.Count == 0)
        {
            _contributions.Remove(fullPath);
        }
        else
        {
            _contributions[fullPath] = newFlags;
        }

        void Decrement(string flag)
        {
            if (!_baseCounts.TryGetValue(flag, out var count))
            {
                return;
            }

            if (count <= 1)
            {
                _baseCounts.Remove(flag);
            }
            else
            {
                _baseCounts[flag] = count - 1;
            }
        }
    }

    private WorkspaceDetectionResult BuildResult()
    {
        var active = new HashSet<string>(_baseCounts.Keys, StringComparer.Ordinal);

        if (_hasGit)
        {
            active.Add(HasGitFlag);
        }

        var changed = true;

        while (changed)
        {
            changed = false;

            foreach (var edge in _flagActivationEdges)
            {
                if (active.Contains(edge.Child) && active.Add(edge.Parent))
                {
                    changed = true;
                }
            }
        }

        return new WorkspaceDetectionResult { Flags = active.ToFrozenSet(StringComparer.Ordinal) };
    }

    private async Task<HashSet<string>> ClassifyFileAsync(
        string fullPath, string relativePath, CancellationToken cancellationToken)
    {
        var flags = new HashSet<string>(StringComparer.Ordinal);
        var fileName = Path.GetFileName(fullPath);
        var extension = ExtensionOf(fileName);

        if (extension.Length > 0 && _extensionToFlags.TryGetValue(extension, out var extensionFlags))
        {
            foreach (var flag in extensionFlags)
            {
                flags.Add(flag);
            }
        }

        if (_fileNameToFlags.TryGetValue(fileName, out var nameFlags))
        {
            foreach (var flag in nameFlags)
            {
                flags.Add(flag);
            }
        }

        foreach (var (pattern, flag) in _globRules)
        {
            if (pattern.IsMatch(relativePath))
            {
                flags.Add(flag);
            }
        }

        if (string.Equals(fileName, PackageJsonFileName, StringComparison.OrdinalIgnoreCase))
        {
            flags.Add(HasNodeJsFlag);
        }

        string? content = null;

        foreach (var (extensions, fileNames, rules) in _contentScans)
        {
            if (!extensions.Contains(extension) && !fileNames.Contains(fileName))
            {
                continue;
            }

            content ??= await TryReadAsync(fullPath, cancellationToken).ConfigureAwait(false);

            if (content is null)
            {
                break;
            }

            foreach (var rule in rules)
            {
                if (rule.Pattern.IsMatch(content))
                {
                    flags.Add(rule.Flag);
                }
            }
        }

        return flags;
    }

    private void Enqueue(string fullPath)
    {
        var relativePath = RelativePath(fullPath);

        if (!IsRelevant(fullPath, relativePath))
        {
            return;
        }

        lock (_pendingLock)
        {
            _pendingPaths.Add(fullPath);
        }

        _debouncer.Signal();
    }

    private IEnumerable<string> EnumerateWorkspaceFiles(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_workspacePath))
        {
            yield break;
        }

        var stack = new Stack<string>();
        stack.Push(_workspacePath);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = stack.Pop();

            foreach (var subdirectory in SafeEnumerate(directory, isDirectory: true))
            {
                if (!ExcludedDirectories.Contains(Path.GetFileName(subdirectory)))
                {
                    stack.Push(subdirectory);
                }
            }

            foreach (var file in SafeEnumerate(directory, isDirectory: false))
            {
                yield return file;
            }
        }
    }

    [SuppressMessage("Design", "CA1031",
        Justification = "Debouncer callback boundary: any reclassification failure must be logged and swallowed, never fault the consumer loop.")]
    private async Task FlushAsync(CancellationToken cancellationToken)
    {
        string[] paths;

        lock (_pendingLock)
        {
            if (_pendingPaths.Count == 0)
            {
                return;
            }

            paths = [.. _pendingPaths];
            _pendingPaths.Clear();
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            foreach (var fullPath in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var flags = File.Exists(fullPath)
                    ? await ClassifyFileAsync(fullPath, RelativePath(fullPath), cancellationToken).ConfigureAwait(false)
                    : new HashSet<string>(StringComparer.Ordinal);

                ApplyContribution(fullPath, flags);
            }

            PublishIfChanged(BuildResult());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (!IsCritical(ex))
        {
            LogIncrementalDetectionFailed(_logger, ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool IsRelevant(string fullPath, string relativePath)
    {
        if (IsExcluded(relativePath))
        {
            return false;
        }

        var fileName = Path.GetFileName(fullPath);
        var extension = ExtensionOf(fileName);

        if (extension.Length > 0 && _extensionToFlags.ContainsKey(extension))
        {
            return true;
        }

        if (_fileNameToFlags.ContainsKey(fileName))
        {
            return true;
        }

        if (string.Equals(fileName, PackageJsonFileName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var (extensions, fileNames, _) in _contentScans)
        {
            if (extensions.Contains(extension) || fileNames.Contains(fileName))
            {
                return true;
            }
        }

        foreach (var (pattern, _) in _globRules)
        {
            if (pattern.IsMatch(relativePath))
            {
                return true;
            }
        }

        return false;
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
        => Enqueue(e.FullPath);

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        Enqueue(e.OldFullPath);
        Enqueue(e.FullPath);
    }

    private void PublishIfChanged(WorkspaceDetectionResult result)
    {
        if (_current.Flags.SetEquals(result.Flags))
        {
            return;
        }

        Volatile.Write(ref _current, result);
        Changed?.Invoke(this, result);
    }

    private string RelativePath(string fullPath)
        => Path.GetRelativePath(_workspacePath, fullPath).Replace('\\', '/');
}
