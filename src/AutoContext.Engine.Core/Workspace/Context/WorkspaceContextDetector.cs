namespace AutoContext.Engine.Core.Workspace.Context;

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

using AutoContext.Engine.Core.Infrastructure;
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
/// the ~40 per-rule globs the extension issues are replaced by the
/// <see cref="WorkspaceFileClassifier"/> lookup index consulted during a
/// single workspace walk. Second, incremental updates use the count-based
/// <see cref="FlagContributionIndex"/> instead of re-globbing on delete:
/// dropping the last contributor for a flag flips it off, while a
/// surviving sibling keeps it on.
/// </para>
/// <para>
/// The recursive <see cref="FileSystemWatcher"/> armed by
/// <see cref="Watch"/> filters events in-code through the same
/// <see cref="WorkspaceFileClassifier"/> that drives classification, so
/// the watch surface can never drift from the detection rules. Synthetic
/// flags (<c>hasGit</c>, <c>hasNodeJs</c>) and the cascade-to-fixpoint
/// semantics match the TypeScript source exactly. Detection passes and
/// watcher-driven reclassifications are serialised through an internal
/// gate; reads of <see cref="Current"/> are lock-free.
/// </para>
/// </remarks>
internal sealed partial class WorkspaceContextDetector : IDisposable, IWorkspaceContextAccessor
{
    private const string HasGitFlag = "hasGit";

    private static readonly TimeSpan DefaultDebounceDelay = TimeSpan.FromMilliseconds(500);

    private readonly WorkspaceFileClassifier _classifier;
    private readonly FlagContributionIndex _contributionIndex = new();
    private WorkspaceDetectionResult _current;
    private readonly TrailingEdgeDebouncer _debouncer;
    private bool _disposed;
    private readonly IWorkspaceEngineInfo _engineInfo;
    private readonly FlagExtensionIndex _extensionIndex;
    private readonly IReadOnlyList<FlagActivationEdge> _flagActivationEdges;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _hasGit;
    private readonly ILogger<WorkspaceContextDetector> _logger;
    private readonly Lock _pendingLock = new();
    private readonly HashSet<string> _pendingPaths = new(StringComparer.OrdinalIgnoreCase);
    private long _revision;
    private FileSystemWatcher? _watcher;

    /// <summary>
    /// Creates a detector bound to the workspace described by
    /// <paramref name="engineInfo"/> and builds its classification index
    /// from the supplied rule tables. The detector is inert until
    /// <see cref="DetectAsync"/> seeds the index; arm incremental updates
    /// with <see cref="Watch"/>.
    /// </summary>
    /// <param name="engineInfo">Engine-instance metadata — workspace
    /// path, instance identity/label, and idle timeout — surfaced to
    /// readers via <see cref="EngineInfo"/> and used to locate the
    /// workspace folder to scan. Its
    /// <see cref="IWorkspaceEngineInfo.WorkspacePath"/> must not be
    /// <see langword="null"/> or whitespace.</param>
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
    /// <exception cref="ArgumentNullException"><paramref name="engineInfo"/>,
    /// <paramref name="fileRules"/>, <paramref name="contentScans"/>, or
    /// <paramref name="activationEdges"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="engineInfo"/>'s
    /// workspace path is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="debounceDelay"/> is zero or negative.</exception>
    public WorkspaceContextDetector(
        IWorkspaceEngineInfo engineInfo,
        IReadOnlyList<FilePresenceRule> fileRules,
        IReadOnlyList<ContentScan> contentScans,
        IReadOnlyList<FlagActivationEdge> activationEdges,
        ILogger<WorkspaceContextDetector>? logger = null,
        TimeProvider? timeProvider = null,
        TimeSpan? debounceDelay = null)
    {
        ArgumentNullException.ThrowIfNull(engineInfo);
        ArgumentException.ThrowIfNullOrWhiteSpace(engineInfo.WorkspacePath);
        ArgumentNullException.ThrowIfNull(fileRules);
        ArgumentNullException.ThrowIfNull(contentScans);
        ArgumentNullException.ThrowIfNull(activationEdges);

        if (debounceDelay is { } delay)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
                delay, TimeSpan.Zero, nameof(debounceDelay));
        }

        _engineInfo = engineInfo;
        _logger = logger ?? NullLogger<WorkspaceContextDetector>.Instance;
        _flagActivationEdges = activationEdges;
        _classifier = new WorkspaceFileClassifier(fileRules, contentScans);
        _extensionIndex = new FlagExtensionIndex(fileRules);

        _debouncer = new TrailingEdgeDebouncer(
            FlushAsync, timeProvider ?? TimeProvider.System, debounceDelay ?? DefaultDebounceDelay);
        _current = WorkspaceDetectionResult.Empty;
        _revision = 0;
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
    /// Engine-instance metadata — workspace path, instance
    /// identity/label, and idle timeout — surfaced to <c>Workspace.Info</c>.
    /// </summary>
    public IWorkspaceEngineInfo EngineInfo
        => _engineInfo;

    /// <summary>
    /// Monotonic state-version counter for the snapshot in
    /// <see cref="Current"/>.
    /// </summary>
    public long Revision
        => Volatile.Read(ref _revision);

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
            _contributionIndex.Clear();
            _hasGit = Directory.Exists(Path.Combine(_engineInfo.WorkspacePath, ".git"));

            foreach (var fullPath in WorkspaceFileEnumerator.Walk(_engineInfo.WorkspacePath, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var flags = await _classifier.ClassifyAsync(
                    fullPath, RelativePath(fullPath), cancellationToken).ConfigureAwait(false);

                if (flags.Count > 0)
                {
                    _contributionIndex.Apply(fullPath, flags);
                }
            }

            var result = BuildResult();
            PublishIfChanged(result, publishChangedEvent: false);
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
        if (_watcher is not null || !Directory.Exists(_engineInfo.WorkspacePath))
        {
            return;
        }

        _debouncer.Run();

        var watcher = new FileSystemWatcher(_engineInfo.WorkspacePath)
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

            if (WorkspaceFileEnumerator.IsExcludedDirectoryName(segment))
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

    private WorkspaceDetectionResult BuildResult()
    {
        var active = new HashSet<string>(_contributionIndex.ActiveFlags, StringComparer.Ordinal);

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

        return new WorkspaceDetectionResult
        {
            Flags = active.ToFrozenSet(StringComparer.Ordinal),
            Extensions = _extensionIndex.Resolve(active),
        };
    }

    private void Enqueue(string fullPath)
    {
        var relativePath = RelativePath(fullPath);

        if (IsExcluded(relativePath) || !_classifier.IsRelevant(fullPath, relativePath))
        {
            return;
        }

        lock (_pendingLock)
        {
            _pendingPaths.Add(fullPath);
        }

        _debouncer.Signal();
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
                    ? await _classifier.ClassifyAsync(fullPath, RelativePath(fullPath), cancellationToken).ConfigureAwait(false)
                    : new HashSet<string>(StringComparer.Ordinal);

                _contributionIndex.Apply(fullPath, flags);
            }

            PublishIfChanged(BuildResult(), publishChangedEvent: true);
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

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
        => Enqueue(e.FullPath);

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        Enqueue(e.OldFullPath);
        Enqueue(e.FullPath);
    }

    private void PublishIfChanged(
        WorkspaceDetectionResult result,
        bool publishChangedEvent)
    {
        if (_current.Flags.SetEquals(result.Flags))
        {
            return;
        }

        Volatile.Write(ref _current, result);
        _ = Interlocked.Increment(ref _revision);

        if (publishChangedEvent)
        {
            Changed?.Invoke(this, result);
        }
    }

    private string RelativePath(string fullPath)
        => Path.GetRelativePath(_engineInfo.WorkspacePath, fullPath).Replace('\\', '/');
}
