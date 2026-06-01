namespace AutoContext.Engine.Core.Workspace.Config;

using System.Security.Cryptography;

using AutoContext.Engine.Core.Infrastructure.IO;
using AutoContext.Engine.Core.Workspace.Config.Format;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Owns the <c>.autocontext.json</c> file for a single workspace and
/// keeps an immutable <see cref="ConfigSnapshot"/> snapshot in sync
/// with it. The snapshot is the in-memory source of truth: programmatic
/// edits flow through <see cref="UpdateAsync"/>, which publishes the new
/// snapshot to memory and raises <see cref="Changed"/> before the disk
/// write, while genuine external edits flow in through the optional file
/// watcher and replace the snapshot last-write-wins.
/// </summary>
/// <remarks>
/// <para>
/// Edits, loads, and disk reconciliations are serialised through an
/// internal gate, so the published snapshot and the on-disk file stay
/// coherent even while the watcher is running. Reads of
/// <see cref="Current"/> are always lock-free and return an immutable
/// snapshot that never changes after it is published.
/// </para>
/// <para>
/// The watcher distinguishes its own writes from external edits with a
/// content signature: every write records the signature it produced,
/// and a reconciliation whose on-disk signature matches the last
/// published signature is treated as an echo and ignored.
/// </para>
/// </remarks>
internal sealed partial class ConfigFileManager : IConfigUpdater, IConfigSnapshotAccessor, IDisposable
{
    private const string ConfigFileName = ".autocontext.json";
    private const string DeletedSignature = "<none>";

    private static readonly TimeSpan DefaultBatchWindow = TimeSpan.FromMilliseconds(5);
    private static readonly TimeSpan DefaultDebounceDelay = TimeSpan.FromMilliseconds(100);

    private readonly string _configPath;
    private ConfigSnapshot _current;
    private readonly string _engineVersion;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string _lastSignature = DeletedSignature;
    private readonly ILogger<ConfigFileManager> _logger;
    private readonly FileChangeWatcher _watcher;
    private readonly ConfigBatchWriter _writer;

    /// <summary>
    /// Creates a manager bound to <paramref name="workspacePath"/>'s
    /// <c>.autocontext.json</c>. The snapshot starts empty; call
    /// <see cref="LoadAsync"/> to populate it from disk.
    /// </summary>
    /// <param name="workspacePath">Absolute path of the workspace
    /// folder. Must not be <see langword="null"/> or whitespace.</param>
    /// <param name="engineVersion">Full semver stamped into the
    /// <c>version</c> field on every save and used as the default
    /// version source for newly created entries. Must not be
    /// <see langword="null"/> or whitespace.</param>
    /// <param name="logger">Diagnostic sink. <see langword="null"/>
    /// silences diagnostics.</param>
    /// <param name="timeProvider">Clock that schedules the watcher
    /// debounce window. <see langword="null"/> uses
    /// <see cref="TimeProvider.System"/>.</param>
    /// <param name="debounceDelay">Quiet window the watcher waits for
    /// after the last filesystem event before reconciling, collapsing a
    /// burst of raw events into a single read. <see langword="null"/>
    /// uses 100&#160;ms. Must be positive when supplied.</param>
    /// <param name="batchWindow">In-process window the writer collects
    /// further <see cref="UpdateBatchAsync"/> edits over before folding
    /// them into a single write. <see langword="null"/> uses 5&#160;ms.
    /// Must be positive when supplied.</param>
    /// <exception cref="ArgumentException"><paramref name="workspacePath"/>
    /// or <paramref name="engineVersion"/> is <see langword="null"/>,
    /// empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="debounceDelay"/> or <paramref name="batchWindow"/>
    /// is zero or negative.</exception>
    public ConfigFileManager(
        string workspacePath,
        string engineVersion,
        ILogger<ConfigFileManager>? logger = null,
        TimeProvider? timeProvider = null,
        TimeSpan? debounceDelay = null,
        TimeSpan? batchWindow = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(engineVersion);

        if (debounceDelay is { } delay)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
                delay, TimeSpan.Zero, nameof(debounceDelay));
        }

        if (batchWindow is { } window)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
                window, TimeSpan.Zero, nameof(batchWindow));
        }

        var clock = timeProvider ?? TimeProvider.System;

        _configPath = Path.Combine(workspacePath, ConfigFileName);
        _engineVersion = engineVersion;
        _logger = logger ?? NullLogger<ConfigFileManager>.Instance;
        _watcher = new FileChangeWatcher(
            _configPath,
            ReconcileFromWatcherAsync,
            clock,
            debounceDelay ?? DefaultDebounceDelay);
        _writer = new ConfigBatchWriter(this, clock, batchWindow ?? DefaultBatchWindow);
        _current = ConfigSnapshot.Empty;
    }

    /// <summary>
    /// Raised after a new snapshot is published — by an
    /// <see cref="UpdateAsync"/> edit or by a watcher-driven
    /// reconciliation — carrying the snapshot now in
    /// <see cref="Current"/>. Not raised by <see cref="LoadAsync"/>.
    /// </summary>
    public event EventHandler<ConfigSnapshot>? Changed;

    /// <summary>
    /// Absolute path of the config file this manager owns.
    /// </summary>
    public string ConfigPath
        => _configPath;

    /// <summary>
    /// The snapshot currently held in memory. Each read returns an
    /// immutable value that is safe to use without locking.
    /// </summary>
    public ConfigSnapshot Current
        => Volatile.Read(ref _current);

    /// <inheritdoc />
    public void Dispose()
    {
        _writer.Dispose();
        _watcher.Dispose();
        _gate.Dispose();
    }

    /// <summary>
    /// Reads the config file from disk, replaces the in-memory snapshot
    /// with the result, and returns it. A missing, empty, or malformed
    /// file yields <see cref="ConfigSnapshot.Empty"/>. Does not raise
    /// <see cref="Changed"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The loaded snapshot.</returns>
    public async Task<ConfigSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var (json, signature) = await ReadSnapshotAsync(cancellationToken).ConfigureAwait(false);
            var next = json.ToDomainGraph();

            _lastSignature = signature;
            Volatile.Write(ref _current, next);

            return next;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Re-reads the config file and, when its content differs from the
    /// last published snapshot, replaces the in-memory snapshot and
    /// raises <see cref="Changed"/>. A read whose signature matches the
    /// last published signature — including the echo of this manager's
    /// own write — is ignored. Lets callers pull in external edits on
    /// demand; the file watcher started by <see cref="Watch"/> calls it
    /// automatically.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        ConfigSnapshot next;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var (json, signature) = await ReadSnapshotAsync(cancellationToken).ConfigureAwait(false);

            if (string.Equals(signature, _lastSignature, StringComparison.Ordinal))
            {
                return;
            }

            next = json.ToDomainGraph();
            _lastSignature = signature;
            Volatile.Write(ref _current, next);
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, next);
    }

    /// <summary>
    /// Applies <paramref name="edit"/> to the current snapshot and, when
    /// it produces a different config, publishes the result to memory,
    /// raises <see cref="Changed"/>, and writes it to disk (deleting the
    /// file when nothing is left to store). The edit must return the
    /// snapshot it was given when there is nothing to change, in which
    /// case nothing is published or written.
    /// </summary>
    /// <param name="edit">Pure transform of the current snapshot.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public async Task UpdateAsync(
        Func<ConfigSnapshot, ConfigSnapshot> edit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(edit);

        ConfigSnapshot next;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var current = Current;
            var edited = edit(current);

            if (ReferenceEquals(edited, current))
            {
                return;
            }

            var json = edited.ToFileFormat();
            next = json.IsEmpty ? ConfigSnapshot.Empty : edited with { Version = _engineVersion };
            await PersistAsync(json, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _current, next);
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, next);
    }

    /// <summary>
    /// Queues <paramref name="edit"/> for a coalesced write: edits
    /// enqueued within a short in-process window are folded into a single
    /// <see cref="UpdateAsync"/> call — one disk write, one snapshot swap,
    /// one <see cref="Changed"/> fan-out — applied in enqueue order. Use
    /// this for bulk toggles that arrive as a burst; use
    /// <see cref="UpdateAsync"/> for a single immediate write.
    /// </summary>
    /// <param name="edit">Pure transform of the current snapshot.</param>
    /// <param name="cancellationToken">Drops this edit from its batch if
    /// signalled before the batch is applied.</param>
    /// <returns>A task that completes once the batch containing this edit
    /// has been applied.</returns>
    public Task UpdateBatchAsync(
        Func<ConfigSnapshot, ConfigSnapshot> edit,
        CancellationToken cancellationToken = default)
        => _writer.EnqueueAsync(edit, cancellationToken);

    /// <summary>
    /// Begins watching the config file for external edits, reconciling
    /// the in-memory snapshot whenever a genuine change is detected. Raw
    /// filesystem events are coalesced through a trailing-edge debounce
    /// — a burst of events from a single save reconciles once, after the
    /// debounce window goes quiet. Idempotent; subsequent calls are
    /// no-ops while a watcher is active.
    /// </summary>
    public void Watch()
        => _watcher.Watch();

    private static string ComputeSignature(byte[] bytes)
        => Convert.ToBase64String(SHA256.HashData(bytes));

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Config file at '{Path}' is corrupt; treating it as empty.")]
    private static partial void LogCorruptConfig(ILogger logger, string path);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug,
        Message = "Failed to delete config file at '{Path}'.")]
    private static partial void LogDeleteFailed(ILogger logger, string path, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "Failed to read config file at '{Path}'; treating it as empty.")]
    private static partial void LogReadFailed(ILogger logger, string path, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning,
        Message = "Failed to reconcile config file at '{Path}' after an external change.")]
    private static partial void LogReconcileFailed(ILogger logger, string path, Exception exception);

    private void DeleteFile()
    {
        try
        {
            File.Delete(_configPath);
        }
        catch (IOException ex)
        {
            LogDeleteFailed(_logger, _configPath, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogDeleteFailed(_logger, _configPath, ex);
        }
    }

    private async Task PersistAsync(JsonConfigFile config, CancellationToken cancellationToken)
    {
        if (config.IsEmpty)
        {
            _lastSignature = DeletedSignature;
            DeleteFile();
            return;
        }

        var bytes = ConfigFileFormat.Serialize(config, _engineVersion);
        _lastSignature = ComputeSignature(bytes);

        var stream = new FileStream(
            _configPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        await using (stream.ConfigureAwait(false))
        {
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<byte[]> ReadBytesAsync(CancellationToken cancellationToken)
    {
        var stream = new FileStream(
            _configPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 4096,
            useAsync: true);

        await using (stream.ConfigureAwait(false))
        {
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
            return memory.ToArray();
        }
    }

    private async Task<(JsonConfigFile Json, string Signature)> ReadSnapshotAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_configPath))
        {
            return (JsonConfigFile.Empty, DeletedSignature);
        }

        try
        {
            var bytes = await ReadBytesAsync(cancellationToken).ConfigureAwait(false);
            var signature = ComputeSignature(bytes);

            if (ConfigFileFormat.TryDeserialize(bytes, out var config))
            {
                return (config, signature);
            }

            LogCorruptConfig(_logger, _configPath);
            return (JsonConfigFile.Empty, signature);
        }
        catch (FileNotFoundException)
        {
            return (JsonConfigFile.Empty, DeletedSignature);
        }
        catch (IOException ex)
        {
            LogReadFailed(_logger, _configPath, ex);
            return (JsonConfigFile.Empty, DeletedSignature);
        }
    }

    private async Task ReconcileFromWatcherAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            LogReconcileFailed(_logger, _configPath, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogReconcileFailed(_logger, _configPath, ex);
        }
    }
}
