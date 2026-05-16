namespace AutoContext.Engine.Core.Registry;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Sole owner of the writer surface for <c>engine-registry.json</c>,
/// the machine-wide engine-liveness registry. Applies the
/// <c>design § P9</c> single-writer-per-resource rule on disk:
/// every mutation goes through this surface so the writer mutex,
/// <see cref="FileShare.None"/> lock, exponential-backoff retry,
/// in-place truncate-and-rewrite plus corrupt-file recovery, and
/// schema-version contract are not scattered across consumers.
/// </summary>
/// <remarks>
/// <para>
/// The on-disk shape is a small envelope:
/// <c>{ "schemaVersion": &lt;int&gt;, "entries": [ &lt;RegistryEntry&gt;, … ] }</c>.
/// Wrapping the entry array in an envelope reserves a place for
/// schema evolution; a future schema bump can be detected without
/// inspecting individual entries.
/// </para>
/// <para>
/// Cross-process serialisation is OS-level: <see cref="WriteAsync"/>
/// opens the file with <see cref="FileShare.None"/> for the
/// entire read-modify-write cycle, and a peer that loses the
/// race retries with exponential backoff per
/// <see cref="RegistryFileOptions"/>. In-process serialisation is
/// via an async-compatible mutex so the lock never observes a
/// half-modified file in the success path. The on-disk write is
/// an in-place truncate-and-rewrite, not a temp-file rename, so
/// a crash inside the write window leaves a corrupt file; the
/// next <see cref="WriteAsync"/> call detects the corruption,
/// logs it, and treats the on-disk state as empty so it can
/// re-seed (the "truncate-and-reseed" pitfall in
/// <c>design § engine-registry.json entry lifecycle</c>).
/// </para>
/// <para>
/// The in-process mutex only serialises writers that share the
/// same <see cref="RegistryFileWriter"/> instance. Composition
/// roots must register this type as a singleton (or a keyed
/// singleton per <see cref="Path"/>) so the single-writer-per-
/// resource invariant holds in-process; otherwise the OS lock
/// is the only thing preventing concurrent writers from the
/// same process and the in-process retry loop will be exercised
/// unnecessarily.
/// </para>
/// </remarks>
public sealed partial class RegistryFileWriter : IDisposable
{
    private readonly SemaphoreSlim _writerMutex = new(1, 1);
    private readonly RegistryFileOptions _options;
    private readonly ILogger<RegistryFileWriter> _logger;

    /// <summary>
    /// Creates a new writer bound to <paramref name="path"/>. The
    /// path's parent directory is created lazily on the first
    /// successful write.
    /// </summary>
    /// <param name="path">Absolute path to <c>engine-registry.json</c>.
    /// Must not be <see langword="null"/> or whitespace.</param>
    /// <param name="options">Retry knobs. <see langword="null"/>
    /// uses production defaults from
    /// <see cref="RegistryFileOptions"/>.</param>
    /// <param name="logger">Diagnostic sink for retry exhaustion
    /// and corruption events. <see langword="null"/> silences
    /// diagnostics.</param>
    /// <exception cref="ArgumentException"><paramref name="path"/>
    /// is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="options"/> contains an invalid value.
    /// </exception>
    public RegistryFileWriter(
        string path,
        RegistryFileOptions? options = null,
        ILogger<RegistryFileWriter>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var resolvedOptions = options ?? new RegistryFileOptions();
        resolvedOptions.Validate();

        Path = path;
        _options = resolvedOptions;
        _logger = logger ?? NullLogger<RegistryFileWriter>.Instance;
    }

    /// <summary>
    /// Releases the in-process writer mutex. Safe to call multiple
    /// times. Does not delete or close the underlying registry
    /// file — cross-process readers and writers are unaffected.
    /// </summary>
    public void Dispose() =>
        _writerMutex.Dispose();

    /// <summary>
    /// Absolute path of the registry file this writer owns.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Atomically applies <paramref name="transform"/> to the
    /// current entries and persists the result. The call holds
    /// both the in-process writer mutex and an OS-level
    /// <see cref="FileShare.None"/> lock for the entire
    /// read-modify-write cycle, so no peer observes a
    /// half-modified state in the success path. A crash during
    /// the write window produces a corrupt file; the next call's
    /// read step detects the corruption, logs it, and passes an
    /// empty list to <paramref name="transform"/> so the file is
    /// re-seeded.
    /// </summary>
    /// <param name="transform">Pure function from the current
    /// entries to the new entries. Must not be
    /// <see langword="null"/> and must return a non-null list.
    /// </param>
    /// <param name="cancellationToken">Cancels both the lock-acquire
    /// retry loop and the read step of the write cycle; once the
    /// file has been truncated the write completes
    /// uncancellably so the file is never left empty.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="transform"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="IOException">
    /// The exclusive open retry loop exhausted
    /// <see cref="RegistryFileOptions.MaxAttempts"/> without
    /// acquiring the lock.
    /// </exception>
    public async Task WriteAsync(
        Func<IReadOnlyList<RegistryEntry>, IReadOnlyList<RegistryEntry>> transform,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transform);

        await _writerMutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureDirectoryExists();

            var stream = await OpenExclusiveAsync(cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                var current = await ReadCurrentAsync(stream, cancellationToken).ConfigureAwait(false);
                var next = transform(current);

                await WriteEnvelopeAsync(stream, next, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _writerMutex.Release();
        }
    }

    private void EnsureDirectoryExists()
    {
        var parent = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }
    }

    private async Task<FileStream> OpenExclusiveAsync(CancellationToken cancellationToken)
    {
        var delay = _options.InitialRetryDelay;
        IOException? lastFailure = null;

        for (var attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return new FileStream(
                    Path,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 4096,
                    useAsync: true);
            }
            catch (IOException ex)
            {
                lastFailure = ex;
                if (attempt == _options.MaxAttempts)
                {
                    break;
                }

                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                var doubled = TimeSpan.FromTicks(delay.Ticks * 2);
                delay = doubled > _options.MaxRetryDelay ? _options.MaxRetryDelay : doubled;
            }
        }

        LogExclusiveLockExhausted(_logger, Path, _options.MaxAttempts);
        throw new IOException(
            $"Failed to acquire exclusive lock on engine registry at '{Path}' after {_options.MaxAttempts} attempts.",
            lastFailure);
    }

    private async Task<IReadOnlyList<RegistryEntry>> ReadCurrentAsync(
        FileStream stream,
        CancellationToken cancellationToken)
    {
        if (stream.Length == 0)
        {
            return [];
        }

        stream.Position = 0;
        using var memory = new MemoryStream(capacity: (int)Math.Min(stream.Length, int.MaxValue));
        await stream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
        var bytes = memory.ToArray();

        if (RegistryFileFormat.TryDeserialize(bytes, out var entries, out var onDiskVersion))
        {
            return entries;
        }

        if (onDiskVersion is not 0 and not RegistryFileFormat.CurrentSchemaVersion)
        {
            LogUnknownSchemaVersion(_logger, Path, onDiskVersion, RegistryFileFormat.CurrentSchemaVersion);
        }
        else
        {
            LogCorruptFileRecovery(_logger, Path);
        }

        return [];
    }

    private static async Task WriteEnvelopeAsync(
        FileStream stream,
        IReadOnlyList<RegistryEntry> entries,
        CancellationToken cancellationToken)
    {
        var bytes = RegistryFileFormat.Serialize(entries);

        // Past this point we do not honour cancellation: once the
        // file has been truncated, a cancelled write would leave
        // an empty file that the next reader silently treats as
        // "no entries" — the corrupt-file recovery path is the
        // wrong response because nothing is malformed. Complete
        // the write or surface the I/O failure.
        cancellationToken.ThrowIfCancellationRequested();
        stream.Position = 0;
        stream.SetLength(0);
        await stream.WriteAsync(bytes, CancellationToken.None).ConfigureAwait(false);
        await stream.FlushAsync(CancellationToken.None).ConfigureAwait(false);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Failed to acquire exclusive lock on engine registry at '{Path}' after {Attempts} attempts.")]
    private static partial void LogExclusiveLockExhausted(ILogger logger, string path, int attempts);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "Engine registry at '{Path}' was unparseable; treating as empty and re-seeding (corrupt-file recovery).")]
    private static partial void LogCorruptFileRecovery(ILogger logger, string path);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning,
        Message = "Engine registry at '{Path}' carries schema version {OnDiskVersion}; expected {CurrentVersion}. Treating as empty and re-seeding.")]
    private static partial void LogUnknownSchemaVersion(ILogger logger, string path, int onDiskVersion, int currentVersion);
}
