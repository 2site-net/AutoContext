namespace AutoContext.Engine.Core.Registry;

using AutoContext.Engine.Protocol.Messages.Registry;

using Microsoft.Extensions.Logging;

/// <summary>
/// Atomic single-shot writer for <c>engine-registry.json</c>.
/// Persists a snapshot of entries via the temp-file + fsync +
/// rename pattern so a crash inside the write window cannot leave
/// the real file truncated or partially written — readers always
/// observe either the previous content or the new content.
/// </summary>
/// <remarks>
/// <para>
/// This type is intentionally narrow: it knows how to atomically
/// write one snapshot of entries to disk. It does <b>not</b>
/// coordinate concurrent writers (in-process or cross-process),
/// does <b>not</b> read the current file, and does <b>not</b>
/// implement the read-modify-write cycle. Those responsibilities
/// live in <see cref="RegistryFileService"/>, which is the only
/// intended caller of this type — hence <see langword="internal"/>.
/// </para>
/// <para>
/// Thread-safety: instances are stateless after construction and
/// can be invoked from any thread. Concurrent invocations against
/// the same path produce two independent temp files that race on
/// the final rename — last-rename-wins, no torn file, but lost
/// updates are possible. Callers that need ordered, lost-update-free
/// writes must serialise through <see cref="RegistryFileService"/>.
/// </para>
/// </remarks>
internal sealed partial class RegistryFileWriter
{
    private readonly ILogger<RegistryFileWriter> _logger;

    /// <summary>
    /// Creates a new writer bound to <paramref name="path"/>.
    /// </summary>
    /// <param name="path">Absolute path to the registry file.
    /// Must not be <see langword="null"/> or whitespace.</param>
    /// <param name="logger">Diagnostic sink.</param>
    /// <exception cref="ArgumentException"><paramref name="path"/>
    /// is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="logger"/> is <see langword="null"/>.</exception>
    public RegistryFileWriter(
        string path,
        ILogger<RegistryFileWriter> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(logger);

        Path = path;
        _logger = logger;
    }

    /// <summary>
    /// Absolute path of the registry file this writer targets.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Synchronously writes <paramref name="entries"/> to
    /// <see cref="Path"/> via the atomic temp-file + fsync +
    /// rename pattern. The real file is either left at its prior
    /// content (on any failure before the rename completes) or
    /// replaced with the new content (after the rename returns).
    /// </summary>
    /// <param name="entries">Entries to persist; must not be
    /// <see langword="null"/>. An empty list is valid and writes
    /// an envelope with no entries.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="entries"/> is <see langword="null"/>.</exception>
    /// <exception cref="IOException">A filesystem operation
    /// (create, write, flush, rename) failed. The temp file is
    /// best-effort deleted before the exception propagates.</exception>
    public void Write(IReadOnlyList<JsonRegistryEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        EnsureParentDirectoryExists();
        var bytes = RegistryFileFormat.Serialize(entries);
        var tempPath = ComposeTempPath();

        try
        {
            using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: false))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, Path, overwrite: true);
        }
        catch
        {
            TryDeleteTemp(tempPath);
            throw;
        }
    }

    private void EnsureParentDirectoryExists()
    {
        var parent = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }
    }

    private string ComposeTempPath() =>
        $"{Path}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";

    private void TryDeleteTemp(string tempPath)
    {
        try
        {
            File.Delete(tempPath);
        }
        catch (IOException ex)
        {
            LogTempCleanupFailed(_logger, tempPath, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogTempCleanupFailed(_logger, tempPath, ex);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Failed to delete registry temp file '{TempPath}' during error rollback.")]
    private static partial void LogTempCleanupFailed(ILogger logger, string tempPath, Exception exception);
}
