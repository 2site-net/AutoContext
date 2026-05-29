namespace AutoContext.Engine.Core.Machine.Housekeeping;

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Hosted service that runs the engine's cache-root shutdown
/// sweep. <see cref="StartAsync(CancellationToken)"/> is a no-op;
/// all work happens in <see cref="StopAsync(CancellationToken)"/>
/// under a ≤ 1 s deadline per <c>design § Housekeeping</c>.
/// </summary>
/// <remarks>
/// <para>
/// No startup sweep: under the per-launch-UUID contract every
/// engine's <c>&lt;instanceId&gt;</c> is fresh on every spawn, so
/// the registry stays append-only and there is nothing to
/// reconcile before pipe-bind. The shutdown sweep alone keeps the
/// cache root self-cleaning — whatever the sweep doesn't reach
/// this time, the next graceful shutdown of any peer catches.
/// </para>
/// <para>
/// Registration order pins the invariant: this service is
/// registered <i>after</i> <c>RegistryFileService</c> (and before
/// <c>LifecycleService</c>) so the host stops it <i>before</i> the
/// registry file service — the sweep can still observe the
/// on-disk registry in its post-pipe-close shape, while
/// <c>LifecycleService</c> has already torn down the four pipes.
/// </para>
/// <para>
/// Sweeps are best-effort. Cancellation (the host's shutdown
/// deadline or this service's own internal deadline, whichever
/// fires first) is honoured between subtrees; an in-flight
/// <see cref="Directory.Delete(string, bool)"/> is not aborted, so
/// a sweep can overshoot the deadline by at most one large
/// subtree. Per-subtree failures are logged and swallowed so the
/// host always shuts down cleanly.
/// </para>
/// </remarks>
internal sealed partial class HousekeepingService : IHostedService
{
    /// <summary>
    /// Upper bound on how long the shutdown sweep is allowed to
    /// run — the ≤ 1 s budget <c>design § Housekeeping</c>
    /// specifies. Host-shutdown latency is bounded by this even if
    /// the cache root contains thousands of stale subtrees.
    /// </summary>
    public static readonly TimeSpan ShutdownDeadline = TimeSpan.FromSeconds(1);

    private readonly CacheRootScanner _scanner;
    private readonly StaleSubtreeCleaner _cleaner;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<HousekeepingService> _logger;

    /// <summary>
    /// Creates a new <see cref="HousekeepingService"/>.
    /// </summary>
    /// <param name="scanner">Cache-root classifier consumed by the
    /// sweep.</param>
    /// <param name="cleaner">Per-subtree deleter the sweep
    /// dispatches to.</param>
    /// <param name="timeProvider">Clock source for the
    /// <see cref="ShutdownDeadline"/> timer; tests substitute a
    /// frozen clock so the deadline is observable without
    /// real-time waits.</param>
    /// <param name="logger">Diagnostic sink.</param>
    /// <exception cref="ArgumentNullException">
    /// Any argument is <see langword="null"/>.
    /// </exception>
    public HousekeepingService(
        CacheRootScanner scanner,
        StaleSubtreeCleaner cleaner,
        TimeProvider timeProvider,
        ILogger<HousekeepingService> logger)
    {
        ArgumentNullException.ThrowIfNull(scanner);
        ArgumentNullException.ThrowIfNull(cleaner);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _scanner = scanner;
        _cleaner = cleaner;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>No-op — the engine's housekeeping is shutdown-only.</summary>
    /// <param name="cancellationToken">Ignored.</param>
    /// <returns>A completed task.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Runs the cache-root shutdown sweep under the
    /// <see cref="ShutdownDeadline"/> budget. Always returns
    /// successfully; failures inside the sweep are logged.
    /// </summary>
    /// <param name="cancellationToken">Outer host-shutdown
    /// deadline. Combined with the internal
    /// <see cref="ShutdownDeadline"/> via a linked CTS — whichever
    /// fires first ends the sweep.</param>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The shutdown sweep is best-effort and must never block host shutdown — any sweep-level failure (registry-read I/O, scanner enumeration, unexpected cleaner fault) is logged and swallowed so the host's StopAsync chain continues.")]
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        using var deadlineCts = new CancellationTokenSource(
            ShutdownDeadline, _timeProvider);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, deadlineCts.Token);

        try
        {
            var classifications = await _scanner
                .ScanAsync(linkedCts.Token)
                .ConfigureAwait(false);

            var deleted = _cleaner.Sweep(classifications, linkedCts.Token);

            LogSweepCompleted(_logger, classifications.Count, deleted);
        }
        catch (OperationCanceledException)
        {
            LogSweepDeadlineExceeded(_logger);
        }
        catch (Exception ex)
        {
            LogSweepFaulted(_logger, ex);
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Housekeeping shutdown sweep classified {Classified} subtrees and reaped {Deleted}.")]
    private static partial void LogSweepCompleted(ILogger logger, int classified, int deleted);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Housekeeping shutdown sweep exceeded its ≤ 1 s deadline; the next peer's sweep will continue.")]
    private static partial void LogSweepDeadlineExceeded(ILogger logger);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Housekeeping shutdown sweep faulted; the next peer's sweep will retry.")]
    private static partial void LogSweepFaulted(ILogger logger, Exception exception);
}
