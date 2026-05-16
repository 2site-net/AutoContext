namespace AutoContext.Engine.Core.Registry;

using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// Alias the BCL path helpers because this type exposes a `Path`
// property that shadows `System.IO.Path` inside member bodies.
using IOPath = System.IO.Path;

/// <summary>
/// Hosted service that owns every form of coordination required to
/// safely mutate <c>engine-registry.json</c>: a single dedicated
/// worker thread for in-process serialisation, a named OS mutex
/// for cross-process serialisation, and the read-modify-write
/// cycle that composes the passive <see cref="RegistryFileReader"/>
/// with the atomic <see cref="RegistryFileWriter"/>.
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
/// <see cref="StartAsync"/> spins up the worker thread;
/// <see cref="StopAsync"/> closes the channel, drains pending
/// writes up to <see cref="RegistryFileServiceOptions.ShutdownDrainTimeout"/>,
/// and cancels stragglers.
/// </para>
/// </remarks>
public sealed partial class RegistryFileService : IHostedService, IAsyncDisposable
{
    private readonly RegistryFileServiceOptions _options;
    private readonly RegistryFileReader _reader;
    private readonly RegistryFileWriter _writer;
    private readonly Mutex _crossProcessMutex;
    private readonly Channel<WriteRequest> _channel;
    private readonly ILogger<RegistryFileService> _logger;
    private Thread? _workerThread;
    private CancellationTokenSource? _stoppingCts;
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
    /// <exception cref="ArgumentException"><paramref name="path"/>
    /// is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An option
    /// value is invalid.</exception>
    public RegistryFileService(
        string path,
        RegistryFileServiceOptions? serviceOptions = null,
        RegistryFileReaderOptions? readerOptions = null,
        ILoggerFactory? loggerFactory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var resolvedOptions = serviceOptions ?? new RegistryFileServiceOptions();
        resolvedOptions.Validate();

        var factory = loggerFactory ?? NullLoggerFactory.Instance;

        Path = path;
        _options = resolvedOptions;
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
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var thread = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "ac-registry-writer",
        };
        var cts = new CancellationTokenSource();

        // Publish _stoppingCts before _workerThread so the worker
        // observes a non-null token source. Reserve the thread slot
        // atomically so concurrent StartAsync calls fail loudly.
        if (Interlocked.CompareExchange(ref _workerThread, thread, null) is not null)
        {
            cts.Dispose();
            throw new InvalidOperationException(
                $"RegistryFileService for '{Path}' is already started.");
        }

        _stoppingCts = cts;
        thread.Start();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
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

    private sealed record WriteRequest(
        Func<IReadOnlyList<RegistryEntry>, IReadOnlyList<RegistryEntry>> Transform,
        TaskCompletionSource Completion,
        CancellationToken CancellationToken);
}
