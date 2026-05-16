namespace AutoContext.Engine.Core.Registry;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Read-only accessor for <c>engine-registry.json</c>. Opens the
/// file with <see cref="FileShare.ReadWrite"/> so readers never
/// contend with one another (P9 readers-are-concurrent rule) and
/// uses an exponential-backoff retry loop to wait out a writer
/// that holds the file with <see cref="FileShare.None"/>.
/// </summary>
/// <remarks>
/// <para>
/// This type is stateless and has no in-process synchronisation:
/// instantiate freely (per call, per scope, or as a singleton).
/// It does not own an OS handle or a mutex, so it is not
/// <see cref="IDisposable"/>.
/// </para>
/// <para>
/// Corruption is observed silently: a missing file, an empty
/// file, malformed JSON, or an unknown schema version all yield
/// an empty list. The writer is the one that re-seeds on its
/// next call and emits a diagnostic when it does — readers are
/// observers, not recovery agents. The one exception is an
/// unknown schema version, which is logged so a stale reader can
/// be diagnosed in the field.
/// </para>
/// </remarks>
public sealed partial class RegistryFileReader
{
    private readonly RegistryFileOptions _options;
    private readonly ILogger<RegistryFileReader> _logger;

    /// <summary>
    /// Creates a new reader bound to <paramref name="path"/>.
    /// </summary>
    /// <param name="path">Absolute path to <c>engine-registry.json</c>.
    /// Must not be <see langword="null"/> or whitespace.</param>
    /// <param name="options">Retry knobs. <see langword="null"/>
    /// uses production defaults from
    /// <see cref="RegistryFileOptions"/>.</param>
    /// <param name="logger">Diagnostic sink. <see langword="null"/>
    /// silences diagnostics.</param>
    /// <exception cref="ArgumentException"><paramref name="path"/>
    /// is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="options"/> contains an invalid value.
    /// </exception>
    public RegistryFileReader(
        string path,
        RegistryFileOptions? options = null,
        ILogger<RegistryFileReader>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var resolvedOptions = options ?? new RegistryFileOptions();
        resolvedOptions.Validate();

        Path = path;
        _options = resolvedOptions;
        _logger = logger ?? NullLogger<RegistryFileReader>.Instance;
    }

    /// <summary>
    /// Absolute path of the registry file this reader observes.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Returns a snapshot of the current entries. A missing,
    /// empty, malformed, or wrong-schema file all yield an empty
    /// list.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Frozen list of entries.</returns>
    /// <exception cref="IOException">
    /// The retry loop exhausted
    /// <see cref="RegistryFileOptions.MaxAttempts"/> without
    /// acquiring a shared read handle.
    /// </exception>
    public async Task<IReadOnlyList<RegistryEntry>> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(Path))
        {
            return [];
        }

        var delay = _options.InitialRetryDelay;
        IOException? lastFailure = null;

        for (var attempt = 1; attempt <= _options.MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                byte[] bytes;
                var stream = new FileStream(
                    Path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite,
                    bufferSize: 4096,
                    useAsync: true);
                await using (stream.ConfigureAwait(false))
                {
                    using var memory = new MemoryStream();
                    await stream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
                    bytes = memory.ToArray();
                }

                if (RegistryFileFormat.TryDeserialize(bytes, out var entries, out var onDiskVersion))
                {
                    return entries;
                }

                // An unknown schema version is worth logging:
                // it signals a peer running a newer engine build.
                // Generic corruption is intentionally not logged
                // on the read path — the writer emits the
                // recovery diagnostic once on its next mutate.
                if (onDiskVersion is not 0 and not RegistryFileFormat.CurrentSchemaVersion)
                {
                    LogUnknownSchemaVersion(_logger, Path, onDiskVersion, RegistryFileFormat.CurrentSchemaVersion);
                }

                return [];
            }
            catch (FileNotFoundException)
            {
                return [];
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

        LogReadLockExhausted(_logger, Path, _options.MaxAttempts);
        throw new IOException(
            $"Failed to acquire shared read handle on engine registry at '{Path}' after {_options.MaxAttempts} attempts.",
            lastFailure);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Failed to acquire shared read handle on engine registry at '{Path}' after {Attempts} attempts.")]
    private static partial void LogReadLockExhausted(ILogger logger, string path, int attempts);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "Engine registry at '{Path}' carries schema version {OnDiskVersion}; expected {CurrentVersion}. Treating as empty.")]
    private static partial void LogUnknownSchemaVersion(ILogger logger, string path, int onDiskVersion, int currentVersion);
}
