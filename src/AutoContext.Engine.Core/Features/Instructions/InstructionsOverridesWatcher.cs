namespace AutoContext.Engine.Core.Features.Instructions;

using AutoContext.Engine.Core.Features.Instructions.Snapshot;
using AutoContext.Engine.Core.Infrastructure.Events;

using Microsoft.Extensions.Logging;

/// <summary>
/// Watches one or more workspace-relative override directories for
/// external edits and keeps an immutable
/// <see cref="InstructionsOverridesSnapshot"/> inventory in sync with them. The
/// directories are supplied by the caller — typically the resolved
/// <c>InstructionsOverridesRoots</c> from a workspace's
/// <c>.autocontext.json</c> engine settings — so the watcher itself
/// is directory-agnostic and never assumes a particular layout such as
/// <c>.github</c>. Each supplied root <c>R</c> contributes the override
/// files under <c>&lt;workspace&gt;/R/instructions/*.instructions.md</c>.
/// The inventory is the in-memory source of truth for which bundled
/// instructions files a workspace-local copy shadows: the initial scan
/// runs through <see cref="LoadAsync"/>, and later filesystem changes
/// flow in through the watcher, which collapses a burst of raw events
/// into a single rescan via a <see cref="TrailingEdgeDebouncer"/> and
/// republishes the inventory last-write-wins.
/// </summary>
/// <remarks>
/// <para>
/// Scans and reconciliations are serialised through an internal gate, so
/// the published inventory and the on-disk directories stay coherent even
/// while the watcher is running. Reads of <see cref="Current"/> are
/// always lock-free and return an immutable snapshot that never changes
/// after it is published.
/// </para>
/// <para>
/// Override directories are frequently absent, so the watcher never
/// watches recursively and never assumes any ancestor exists. Each
/// configured directory is watched by its own independent cascade: it
/// arms on the deepest directory that currently exists between its
/// <c>instructions/</c> scan directory and the workspace root and, on
/// each settled burst, re-arms one level deeper as the next directory
/// appears. Watching each level non-recursively keeps the OS watch scope
/// bounded to a single directory's immediate children — it never descends
/// into <c>node_modules</c>, <c>.git</c>, or build output — while still
/// surfacing a later-created override directory live. Every settled burst
/// republishes the inventory and raises <see cref="Changed"/>, including
/// content-only edits to an existing override, so downstream projection
/// always observes the latest bytes.
/// </para>
/// <para>
/// When the same <c>*.instructions.md</c> basename appears under more
/// than one configured directory, the first directory in the supplied
/// precedence order wins and the shadowed copies are logged and ignored.
/// </para>
/// </remarks>
internal sealed partial class InstructionsOverridesWatcher : IDisposable, IInstructionsOverridesAccessor
{
    private const string InstructionsSubdirectory = "instructions";
    private const string OverridePattern = "*.instructions.md";

    /// <summary>
    /// Default quiet window the watcher waits for after the last
    /// filesystem event before rescanning, matching the engine's other
    /// file watchers.
    /// </summary>
    internal static readonly TimeSpan DefaultDebounceDelay = TimeSpan.FromMilliseconds(100);

    private InstructionsOverridesSnapshot _current = InstructionsOverridesSnapshot.Empty;
    private readonly TrailingEdgeDebouncer _debouncer;
    private bool _disposed;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger<InstructionsOverridesWatcher> _logger;
    private readonly List<string> _overrideDirectories;
    private readonly IReadOnlyList<OverrideDirectoryWatch> _watches;

    /// <summary>
    /// Creates a watcher over <paramref name="instructionsOverridesRoots"/>,
    /// resolved relative to <paramref name="workspacePath"/>. Each entry
    /// is a directory whose <c>instructions/</c> subfolder holds the
    /// override files; the entries are taken in precedence order. The
    /// inventory starts empty; call <see cref="LoadAsync"/> to populate it
    /// from disk.
    /// </summary>
    /// <param name="workspacePath">Absolute path of the workspace
    /// folder. Must not be <see langword="null"/> or whitespace.</param>
    /// <param name="instructionsOverridesRoots">Workspace-relative override
    /// roots, in precedence order. Blank entries, duplicates, and entries
    /// that escape the workspace are skipped. Must not be
    /// <see langword="null"/>.</param>
    /// <param name="timeProvider">Clock that schedules the watcher
    /// debounce window.</param>
    /// <param name="debounceDelay">Quiet window the watcher waits for
    /// after the last filesystem event before rescanning, collapsing a
    /// burst of raw events into a single scan. Must be positive.</param>
    /// <param name="logger">Diagnostic sink.</param>
    /// <exception cref="ArgumentException"><paramref name="workspacePath"/>
    /// is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="instructionsOverridesRoots"/>,
    /// <paramref name="timeProvider"/>, or <paramref name="logger"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="debounceDelay"/> is zero or negative.</exception>
    public InstructionsOverridesWatcher(
        string workspacePath,
        IReadOnlyList<string> instructionsOverridesRoots,
        TimeProvider timeProvider,
        TimeSpan debounceDelay,
        ILogger<InstructionsOverridesWatcher> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentNullException.ThrowIfNull(instructionsOverridesRoots);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            debounceDelay, TimeSpan.Zero, nameof(debounceDelay));
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _debouncer = new TrailingEdgeDebouncer(ReconcileFromWatcherAsync, timeProvider, debounceDelay);

        var workspaceRoot = Path.GetFullPath(workspacePath);
        _overrideDirectories = ResolveScanDirectories(workspaceRoot, instructionsOverridesRoots);
        _watches = [.. _overrideDirectories.Select(directory =>
            new OverrideDirectoryWatch(directory, workspaceRoot, _debouncer.Signal))];
    }

    /// <summary>
    /// Raised after a watcher-driven rescan republishes the inventory,
    /// carrying the snapshot now in <see cref="Current"/>. Not raised by
    /// <see cref="LoadAsync"/>.
    /// </summary>
    public event EventHandler<InstructionsOverridesSnapshot>? Changed;

    /// <inheritdoc />
    /// <remarks>
    /// Lock-free; returns an immutable snapshot that never changes after
    /// it is published.
    /// </remarks>
    public InstructionsOverridesSnapshot Current => Volatile.Read(ref _current);

    /// <summary>
    /// The absolute scan directories this watcher owns, in precedence
    /// order — each the <c>instructions/</c> subfolder of a configured
    /// override root.
    /// </summary>
    public IReadOnlyList<string> OverrideDirectories => _overrideDirectories;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _debouncer.Dispose();

        foreach (var watch in _watches)
        {
            watch.Dispose();
        }

        _gate.Dispose();
    }

    /// <summary>
    /// Scans the override directories and publishes the resulting
    /// inventory to <see cref="Current"/> without raising
    /// <see cref="Changed"/>. Returns an empty inventory when no override
    /// directory exists.
    /// </summary>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <returns>The inventory now in <see cref="Current"/>.</returns>
    public async Task<InstructionsOverridesSnapshot> LoadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var overrides = Scan();
            Volatile.Write(ref _current, overrides);
            return overrides;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Rescans the override directories, publishes the resulting
    /// inventory to <see cref="Current"/>, and raises
    /// <see cref="Changed"/> with it. Drives both the watcher callback and
    /// explicit reconciliation.
    /// </summary>
    /// <param name="cancellationToken">Cancels the scan.</param>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        InstructionsOverridesSnapshot overrides;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            overrides = Scan();
            Volatile.Write(ref _current, overrides);
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, overrides);
    }

    /// <summary>
    /// Begins watching for external override edits, forwarding each raw
    /// filesystem event into the debounce window so settled changes flow
    /// through <see cref="RefreshAsync"/>. Each configured directory arms
    /// on its own deepest currently existing ancestor and re-arms deeper
    /// as the intermediate directories are created. Idempotent;
    /// subsequent calls re-evaluate the watch roots rather than
    /// duplicating watchers.
    /// </summary>
    public void Watch()
    {
        _debouncer.Run();
        Arm();
    }

    private static List<string> ResolveScanDirectories(
        string workspaceRoot, IReadOnlyList<string> instructionsOverridesRoots)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var directories = new List<string>(instructionsOverridesRoots.Count);

        foreach (var root in instructionsOverridesRoots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            var scanDirectory = Path.GetFullPath(
                Path.Combine(workspaceRoot, root, InstructionsSubdirectory));

            if (!scanDirectory.StartsWith(
                workspaceRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                continue;
            }

            if (seen.Add(scanDirectory))
            {
                directories.Add(scanDirectory);
            }
        }

        return directories;
    }

    private bool Arm()
    {
        if (_disposed)
        {
            return false;
        }

        var rearmed = false;

        foreach (var watch in _watches)
        {
            rearmed |= watch.Arm();
        }

        return rearmed;
    }

    private InstructionsOverridesSnapshot Scan()
    {
        if (_overrideDirectories.Count == 0)
        {
            return InstructionsOverridesSnapshot.Empty;
        }

        var pathsByFileName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in _overrideDirectories)
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(
                directory, OverridePattern, SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileName(path);

                if (pathsByFileName.TryGetValue(fileName, out var winning))
                {
                    LogOverrideConflict(_logger, fileName, winning, path);
                    continue;
                }

                pathsByFileName[fileName] = path;
            }
        }

        return pathsByFileName.Count == 0
            ? InstructionsOverridesSnapshot.Empty
            : new InstructionsOverridesSnapshot(pathsByFileName);
    }

    private async Task ReconcileFromWatcherAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RefreshAsync(cancellationToken).ConfigureAwait(false);

            if (Arm())
            {
                _debouncer.Signal();
            }
        }
        catch (OperationCanceledException)
        {
            // Watcher teardown requested; drop the in-flight rescan.
        }
        catch (IOException exception)
        {
            LogRescanFailed(_logger, exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            LogRescanFailed(_logger, exception);
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Failed to rescan instruction overrides.")]
    private static partial void LogRescanFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Instruction override '{FileName}' is shadowed: using '{WinningPath}' and ignoring '{IgnoredPath}' from a lower-precedence directory.")]
    private static partial void LogOverrideConflict(ILogger logger, string fileName, string winningPath, string ignoredPath);

    /// <summary>
    /// Owns the non-recursive cascading <see cref="FileSystemWatcher"/>
    /// for a single override scan directory. Arms on the deepest existing
    /// directory between the scan directory and the workspace root and
    /// re-arms deeper as intermediate directories appear, so a
    /// not-yet-created override directory is still surfaced live without
    /// ever watching recursively.
    /// </summary>
    private sealed class OverrideDirectoryWatch(string scanDirectory, string workspacePath, Action onChanged)
        : IDisposable
    {
        private readonly Action _onChanged = onChanged;
        private readonly string _scanDirectory = scanDirectory;
        private readonly Lock _sync = new();
        private readonly string _workspacePath = workspacePath;
        private bool _disposed;
        private FileSystemWatcher? _watcher;
        private string? _watchRoot;

        public bool Arm()
        {
            var root = ResolveWatchRoot();

            lock (_sync)
            {
                if (_disposed || root is null || string.Equals(root, _watchRoot, StringComparison.Ordinal))
                {
                    return false;
                }

                var previous = _watcher;
                _watcher = CreateWatcher(root);
                _watchRoot = root;
                previous?.Dispose();
                return true;
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                _disposed = true;
                _watcher?.Dispose();
                _watcher = null;
            }
        }

        private FileSystemWatcher CreateWatcher(string root)
        {
            var atScanDirectory = string.Equals(root, _scanDirectory, StringComparison.Ordinal);

            var watcher = atScanDirectory
                ? new FileSystemWatcher(root, OverridePattern)
                : new FileSystemWatcher(root);

            watcher.NotifyFilter = NotifyFilters.LastWrite
                | NotifyFilters.FileName
                | NotifyFilters.DirectoryName
                | NotifyFilters.Size;

            watcher.Changed += Raise;
            watcher.Created += Raise;
            watcher.Deleted += Raise;
            watcher.Renamed += Raise;
            watcher.EnableRaisingEvents = true;

            return watcher;
        }

        private void Raise(object sender, FileSystemEventArgs e)
            => _onChanged();

        private string? ResolveWatchRoot()
        {
            var current = _scanDirectory;

            while (true)
            {
                if (Directory.Exists(current))
                {
                    return current;
                }

                if (string.Equals(current, _workspacePath, StringComparison.Ordinal))
                {
                    return null;
                }

                var parent = Path.GetDirectoryName(current);

                if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, StringComparison.Ordinal))
                {
                    return null;
                }

                current = parent;
            }
        }
    }
}
