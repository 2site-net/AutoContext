namespace AutoContext.Engine.Core.Registry;

using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;

using AutoContext.Engine.Protocol.Messages.Registry;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// Alias the BCL path helpers because this type exposes a `Path`
// property that shadows `System.IO.Path` inside member bodies.
using IOPath = Path;

/// <summary>
/// Hosted service that owns every form of coordination required to
/// safely mutate <c>engine-registry.json</c>: a single dedicated
/// worker thread for in-process serialisation, a named OS mutex
/// for cross-process serialisation, and the read-modify-write
/// cycle that composes the passive <see cref="RegistryFileReader"/>
/// with the atomic <see cref="RegistryFileWriter"/>. When
/// configured with an own-entry factory the service also owns the
/// lifecycle of <i>this</i> engine's row: one append on
/// <see cref="StartAsync"/>, one removal on
/// <see cref="StopAsync"/>, never any upsert.
/// </summary>
/// <remarks>
/// <para>
/// This type is the single intended consumer of
/// <see cref="RegistryFileWriter"/>. Application code injects the
/// service and calls <see cref="WriteAsync"/>; the writer is an
/// internal implementation detail of the package.
/// </para>
/// <para>
/// <b>Atomic rename and the cross-process mutex do complementary
/// jobs.</b> The rename in <see cref="RegistryFileWriter"/>
/// eliminates <i>torn files</i> — readers always observe one
/// complete snapshot or another. The mutex eliminates <i>lost
/// updates</i> — without it two peers could each read the prior
/// snapshot, each compute a successor, and each rename, so one
/// successor's contribution would silently vanish. Neither
/// mechanism subsumes the other.
/// </para>
/// <para>
/// <b>Why a dedicated thread.</b> The cross-process synchronisation
/// primitive (<see cref="Mutex"/>) has thread affinity: the same
/// thread that called <see cref="Mutex.WaitOne()"/> must call
/// <see cref="Mutex.ReleaseMutex"/>. Mixing that with
/// <see langword="async"/>/<see langword="await"/> would race with
/// the thread-pool's continuation hopping. The service therefore
/// runs a fully synchronous loop on a single non-pool background
/// thread; the public API remains honestly async via a per-request
/// <see cref="TaskCompletionSource"/>.
/// </para>
/// <para>
/// <b>Lifecycle.</b> Register the service exactly once and surface
/// it as both a singleton and an <see cref="IHostedService"/>:
/// <code>
/// services.AddSingleton&lt;RegistryFileService&gt;();
/// services.AddHostedService(sp => sp.GetRequiredService&lt;RegistryFileService&gt;());
/// </code>
/// <see cref="StartAsync"/> spins up the worker thread and, when an
/// own-entry factory was supplied, appends this engine's row.
/// <see cref="StopAsync"/> removes the own row (best-effort —
/// shutdown deadline cancellations and filesystem hiccups are
/// logged and swallowed, leaving the row for a peer's housekeeping
/// sweep to reap), closes the channel, drains pending writes up to
/// <see cref="RegistryFileServiceOptions.ShutdownDrainTimeout"/>,
/// and cancels stragglers.
/// </para>
/// <para>
/// <b>Stop ordering.</b> The own-entry removal runs
/// <i>before</i> the channel is closed, so it traverses the same
/// worker thread and mutex path as every other write. Peer hosted
/// services that need to write during their own
/// <see cref="StopAsync"/> (Phase 2b housekeeping, future crash
/// writers) must register <i>after</i> this service so they stop
/// <i>before</i> it, hitting a still-live channel.
/// </para>
/// </remarks>
public sealed partial class RegistryFileService : IHostedService, IAsyncDisposable
{
    private readonly RegistryFileServiceOptions _options;
    private readonly RegistryFileReader _reader;
    private readonly RegistryFileWriter _writer;
    private readonly Mutex _crossProcessMutex;
    private readonly Channel<WriteRequest> _channel;
    private readonly Func<RegistryEntry>? _ownEntryFactory;
    private readonly ILogger<RegistryFileService> _logger;
    private Thread? _workerThread;
    private CancellationTokenSource? _stoppingCts;
    private RegistryEntry? _ownEntry;
    private int _disposed;

    /// <summary>
    /// Creates a new service bound to <paramref name="path"/>.
    /// </summary>
    /// <param name="path">Absolute path to <c>engine-registry.json</c>.
    /// Must not be <see langword="null"/> or whitespace.</param>
    /// <param name="serviceOptions">Service-level knobs (mutex
    /// timeout, shutdown drain). <see langword="null"/> uses
    /// defaults.</param>
    /// <param name="readerOptions">Reader retry knobs forwarded
    /// to the inner <see cref="RegistryFileReader"/>.
    /// <see langword="null"/> uses defaults.</param>
    /// <param name="loggerFactory">Factory used to build loggers
    /// for the service, the reader, and the writer.
    /// <see langword="null"/> silences diagnostics.</param>
    /// <param name="ownEntryFactory">Optional factory invoked on
    /// <see cref="StartAsync"/> to build the single row that
    /// represents <i>this</i> engine in the registry. When supplied
    /// the service appends the row on start and removes it on stop
    /// (best-effort). When <see langword="null"/> the service acts
    /// as a plain file coordinator with no own-entry lifecycle —
    /// the convenient shape for tests.</param>
    /// <exception cref="ArgumentException"><paramref name="path"/>
    /// is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An option
    /// value is invalid.</exception>
    public RegistryFileService(
        string path,
        RegistryFileServiceOptions? serviceOptions = null,
        RegistryFileReaderOptions? readerOptions = null,
        ILoggerFactory? loggerFactory = null,
        Func<RegistryEntry>? ownEntryFactory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var resolvedOptions = serviceOptions ?? new RegistryFileServiceOptions();
        resolvedOptions.Validate();

        var factory = loggerFactory ?? NullLoggerFactory.Instance;

        Path = path;
        _options = resolvedOptions;
        _ownEntryFactory = ownEntryFactory;
        _logger = factory.CreateLogger<RegistryFileService>();
        _reader = new RegistryFileReader(path, readerOptions, factory.CreateLogger<RegistryFileReader>());
        _writer = new RegistryFileWriter(path, factory.CreateLogger<RegistryFileWriter>());
        _crossProcessMutex = new Mutex(initiallyOwned: false, ComposeMutexName(path));
        _channel = Channel.CreateUnbounded<WriteRequest>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });
    }

    /// <summary>
    /// Absolute path of the registry file this service guards.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Submits a read-modify-write request and returns a task that
    /// completes when the write has been persisted (or rejected).
    /// Requests are serialised across all callers in this process
    /// and across all peer processes that target the same path.
    /// </summary>
    /// <param name="transform">Pure function that receives the
    /// current snapshot and returns the next snapshot. Must not
    /// be <see langword="null"/>; must not return
    /// <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the request. A
    /// cancellation that lands after the worker has begun
    /// processing is best-effort; it may complete normally.</param>
    /// <returns>Task that completes when the write reaches disk
    /// (atomically replaced via temp+rename), faults on a
    /// filesystem or transform exception, or is cancelled.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="transform"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The service has
    /// stopped and is no longer accepting writes.</exception>
    public Task WriteAsync(
        Func<IReadOnlyList<RegistryEntry>, IReadOnlyList<RegistryEntry>> transform,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transform);
        cancellationToken.ThrowIfCancellationRequested();

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = new WriteRequest(transform, tcs, cancellationToken);
        if (!_channel.Writer.TryWrite(request))
        {
            throw new InvalidOperationException(
                $"RegistryFileService for '{Path}' is not accepting writes; the worker has stopped.");
        }
        return tcs.Task;
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var thread = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "ac-registry-writer",
        };
        var cts = new CancellationTokenSource();

        // Reserve the worker-thread slot atomically so concurrent
        // StartAsync calls fail loudly. _stoppingCts is published
        // before thread.Start(); the Start call is a release fence,
        // so the new thread is guaranteed to observe the non-null
        // token source on its first read.
        if (Interlocked.CompareExchange(ref _workerThread, thread, null) is not null)
        {
            cts.Dispose();
            throw new InvalidOperationException(
                $"RegistryFileService for '{Path}' is already started.");
        }

        _stoppingCts = cts;
        thread.Start();

        if (_ownEntryFactory is { } factory)
        {
            var entry = factory();
            _ownEntry = entry;
            LogAppendingOwnEntry(_logger, Path, entry.InstanceId);
            await WriteAsync(snapshot => Append(snapshot, entry), cancellationToken).ConfigureAwait(false);
            LogAppendedOwnEntry(_logger, Path, entry.InstanceId);
        }
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // Phase 1: best-effort own-entry removal. Must run BEFORE
        // we close the channel; the removal traverses the same
        // worker thread and cross-process mutex as every other
        // write. Failures (shutdown deadline cancellations, fs
        // hiccups, a peer already reaped us) are logged and
        // swallowed — a peer's housekeeping sweep will reap any
        // row we couldn't remove here.
        if (_ownEntry is { } entry)
        {
            _ownEntry = null;
            try
            {
                LogRemovingOwnEntry(_logger, Path, entry.InstanceId);
                await WriteAsync(snapshot => Remove(snapshot, entry.InstanceId), cancellationToken)
                    .ConfigureAwait(false);
                LogRemovedOwnEntry(_logger, Path, entry.InstanceId);
            }
            catch (OperationCanceledException)
            {
                LogOwnEntryRemovalCancelled(_logger, Path, entry.InstanceId);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                LogOwnEntryRemovalFailed(_logger, ex, Path, entry.InstanceId);
            }
#pragma warning restore CA1031
        }

        _channel.Writer.TryComplete();

        var thread = _workerThread;
        if (thread is null)
        {
            // No worker was ever started, so the worker's own
            // finally-block straggler drain will never run. Fail
            // any pre-Start writes here so their callers don't
            // hang forever waiting on a TaskCompletionSource that
            // nothing else will ever complete.
            DrainStragglers();
            return;
        }

        var joined = await Task.Run(
            () => thread.Join(_options.ShutdownDrainTimeout),
            CancellationToken.None).ConfigureAwait(false);

        if (!joined)
        {
            LogShutdownDrainTimedOut(_logger, Path, _options.ShutdownDrainTimeout);
            if (_stoppingCts is { } cts)
            {
                await cts.CancelAsync().ConfigureAwait(false);
            }
            await Task.Run(
                () => thread.Join(TimeSpan.FromSeconds(1)),
                CancellationToken.None).ConfigureAwait(false);
        }

        // Belt-and-braces: if the worker's finally-block drain ran,
        // this is a no-op; if the worker is still wedged past the
        // hard-cancel window, this at least frees callers waiting
        // on items the worker never reached.
        DrainStragglers();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _stoppingCts?.Dispose();
        _crossProcessMutex.Dispose();
    }

    /// <summary>
    /// Computes the name of the cross-process mutex used to guard
    /// <paramref name="path"/>. Exposed as <see langword="internal"/>
    /// so tests can construct a peer mutex with the matching name
    /// to exercise contention scenarios.
    /// </summary>
    internal static string ComposeMutexName(string path)
    {
        // Scope is intentionally session-local — no "Global\"
        // prefix. The engine registry lives under the per-user
        // cache root, so contention only arises between peer
        // engines running as the same user in the same logon
        // session. Cross-session coordination would require
        // SeCreateGlobalPrivilege (admin/service only by default
        // on Windows) and would falsely couple unrelated users'
        // registries.
        //
        // Normalisation is GetFullPath plus case-folding only.
        // Symlinks, junctions, and 8.3 short names are NOT
        // resolved: two paths that traverse the same inode via
        // different surface names will compute different mutex
        // names. The engine cache root never sits behind a
        // reparse point in supported deployments, so this is a
        // deliberate trade-off (resolving the link on every call
        // is an extra I/O hit on the hot write path).
        //
        // ToUpperInvariant per CA1308: this is equality-by-hash,
        // not display normalisation.
        var normalised = OperatingSystem.IsWindows()
            ? IOPath.GetFullPath(path).ToUpperInvariant()
            : IOPath.GetFullPath(path);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalised));
        var hex = Convert.ToHexString(hash)[..16];
        return $"AutoContext.RegistryFile.{hex}";
    }

    private void WorkerLoop()
    {
        var cancelToken = _stoppingCts?.Token ?? CancellationToken.None;

        try
        {
            while (true)
            {
                bool haveMore;
                try
                {
                    haveMore = _channel.Reader.WaitToReadAsync(cancelToken).AsTask().GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ChannelClosedException)
                {
                    break;
                }

                if (!haveMore)
                {
                    break;
                }

                while (_channel.Reader.TryRead(out var request))
                {
                    try
                    {
                        ProcessRequestSync(request);
                    }
                    catch (OperationCanceledException oce)
                    {
                        // Surface cancellation as Canceled rather
                        // than Faulted so callers see Task.IsCanceled.
                        request.Completion.TrySetCanceled(oce.CancellationToken);
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        request.Completion.TrySetException(ex);
                    }
                }
            }
        }
        finally
        {
            DrainStragglers();
        }
    }

    private void DrainStragglers()
    {
        while (_channel.Reader.TryRead(out var straggler))
        {
            straggler.Completion.TrySetCanceled(CancellationToken.None);
        }
    }

    private void ProcessRequestSync(WriteRequest request)
    {
        if (request.CancellationToken.IsCancellationRequested)
        {
            request.Completion.TrySetCanceled(request.CancellationToken);
            return;
        }

        var acquired = false;
        try
        {
            try
            {
                acquired = _crossProcessMutex.WaitOne(_options.MutexAcquireTimeout);
            }
            catch (AbandonedMutexException)
            {
                // Previous holder crashed mid-write. The atomic
                // temp+rename writer guarantees the real file is
                // either intact at the prior content or already
                // replaced with the new content — there is no
                // torn intermediate state to repair. We now own
                // the mutex and may proceed.
                LogPriorWriterAbandonedMutex(_logger, Path);
                acquired = true;
            }

            if (!acquired)
            {
                throw new TimeoutException(
                    $"Failed to acquire cross-process mutex for '{Path}' within {_options.MutexAcquireTimeout}.");
            }

            var current = _reader.ReadAsync(request.CancellationToken).GetAwaiter().GetResult();
            var next = request.Transform(current)
                ?? throw new InvalidOperationException(
                    "RegistryFileService write transform returned null; transforms must return a non-null list.");
            _writer.Write(next);
            request.Completion.TrySetResult();
        }
        finally
        {
            if (acquired)
            {
                _crossProcessMutex.ReleaseMutex();
            }
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Prior writer of engine registry '{Path}' abandoned the cross-process mutex (process likely crashed mid-write); reclaiming.")]
    private static partial void LogPriorWriterAbandonedMutex(ILogger logger, string path);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "RegistryFileService shutdown drain for '{Path}' timed out after {Timeout}; pending writes will be cancelled.")]
    private static partial void LogShutdownDrainTimedOut(ILogger logger, string path, TimeSpan timeout);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug,
        Message = "Appending own engine entry {InstanceId} to registry '{Path}'.")]
    private static partial void LogAppendingOwnEntry(ILogger logger, string path, Guid instanceId);

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug,
        Message = "Appended own engine entry {InstanceId} to registry '{Path}'.")]
    private static partial void LogAppendedOwnEntry(ILogger logger, string path, Guid instanceId);

    [LoggerMessage(EventId = 5, Level = LogLevel.Debug,
        Message = "Removing own engine entry {InstanceId} from registry '{Path}'.")]
    private static partial void LogRemovingOwnEntry(ILogger logger, string path, Guid instanceId);

    [LoggerMessage(EventId = 6, Level = LogLevel.Debug,
        Message = "Removed own engine entry {InstanceId} from registry '{Path}'.")]
    private static partial void LogRemovedOwnEntry(ILogger logger, string path, Guid instanceId);

    [LoggerMessage(EventId = 7, Level = LogLevel.Warning,
        Message = "Own engine entry {InstanceId} removal from registry '{Path}' was cancelled before it landed; leaving the row for a peer's housekeeping sweep to reap.")]
    private static partial void LogOwnEntryRemovalCancelled(ILogger logger, string path, Guid instanceId);

    [LoggerMessage(EventId = 8, Level = LogLevel.Warning,
        Message = "Own engine entry {InstanceId} removal from registry '{Path}' failed; leaving the row for a peer's housekeeping sweep to reap.")]
    private static partial void LogOwnEntryRemovalFailed(ILogger logger, Exception exception, string path, Guid instanceId);

    private static List<RegistryEntry> Append(IReadOnlyList<RegistryEntry> current, RegistryEntry entry)
    {
        // Pre-size: current + the new row. CA1859 prefers the
        // concrete List<T> return type here over IReadOnlyList<T>
        // because callers (the worker-thread transform path) hot-
        // path on the concrete type.
        var next = new List<RegistryEntry>(current.Count + 1);
        next.AddRange(current);
        next.Add(entry);
        return next;
    }

    private static List<RegistryEntry> Remove(IReadOnlyList<RegistryEntry> current, Guid instanceId)
    {
        var next = new List<RegistryEntry>(current.Count);
        foreach (var existing in current)
        {
            if (existing.InstanceId != instanceId)
            {
                next.Add(existing);
            }
        }
        return next;
    }

    private sealed record WriteRequest(
        Func<IReadOnlyList<RegistryEntry>, IReadOnlyList<RegistryEntry>> Transform,
        TaskCompletionSource Completion,
        CancellationToken CancellationToken);
}
