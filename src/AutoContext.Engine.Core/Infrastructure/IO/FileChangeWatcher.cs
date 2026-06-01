namespace AutoContext.Engine.Core.Infrastructure.IO;

using AutoContext.Engine.Core.Infrastructure.Events;

/// <summary>
/// Watches a single file for external edits and invokes a callback once
/// a burst of filesystem events settles. Pairs a
/// <see cref="FileSystemWatcher"/> with a
/// <see cref="TrailingEdgeDebouncer"/>, so the raw <c>Changed</c> /
/// <c>Created</c> / <c>Deleted</c> events from a single save collapse
/// into one callback after the debounce window goes quiet.
/// </summary>
/// <remarks>
/// The watcher owns no reaction logic of its own: it forwards every raw
/// event to the debouncer as a signal and lets the supplied callback
/// decide what a settled change means. The callback runs on the
/// debouncer's consumer loop and must handle its own exceptions.
/// </remarks>
internal sealed class FileChangeWatcher : IDisposable
{
    private readonly TrailingEdgeDebouncer _debouncer;
    private readonly string? _directory;
    private bool _disposed;
    private readonly string _fileName;
    private FileSystemWatcher? _watcher;

    /// <summary>
    /// Creates a watcher for the file at <paramref name="filePath"/>
    /// that invokes <paramref name="onChanged"/> once each settled burst
    /// of filesystem events. The watcher is inert until
    /// <see cref="Watch"/> is called.
    /// </summary>
    /// <param name="filePath">Absolute path of the file to watch. Must
    /// not be <see langword="null"/> or whitespace.</param>
    /// <param name="onChanged">Callback run once per settled burst. Must
    /// handle its own exceptions.</param>
    /// <param name="timeProvider">Clock that schedules the debounce
    /// window.</param>
    /// <param name="debounceDelay">Quiet window to wait for after the
    /// last filesystem event before invoking the callback. Must be
    /// positive.</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/>
    /// is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="onChanged"/>
    /// or <paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="debounceDelay"/> is zero or negative.</exception>
    public FileChangeWatcher(
        string filePath,
        Func<CancellationToken, Task> onChanged,
        TimeProvider timeProvider,
        TimeSpan debounceDelay)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        _directory = Path.GetDirectoryName(filePath);
        _fileName = Path.GetFileName(filePath);
        _debouncer = new TrailingEdgeDebouncer(onChanged, timeProvider, debounceDelay);
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
    }

    /// <summary>
    /// Begins watching the file, forwarding each raw filesystem event
    /// into the debounce window so settled changes surface through the
    /// callback. Idempotent; subsequent calls are no-ops while a watcher
    /// is active. A no-op when the file's directory cannot be resolved.
    /// </summary>
    public void Watch()
    {
        if (_watcher is not null || string.IsNullOrEmpty(_directory))
        {
            return;
        }

        _debouncer.Run();

        var watcher = new FileSystemWatcher(_directory, _fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
        };

        watcher.Changed += OnFileSystemEvent;
        watcher.Created += OnFileSystemEvent;
        watcher.Deleted += OnFileSystemEvent;
        watcher.EnableRaisingEvents = true;

        _watcher = watcher;
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
        => _debouncer.Signal();
}
