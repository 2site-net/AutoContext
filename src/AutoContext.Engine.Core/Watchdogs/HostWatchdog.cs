namespace AutoContext.Engine.Core.Watchdogs;

using System.Diagnostics.CodeAnalysis;

using AutoContext.Engine.Core.Infrastructure.Diagnostics;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Hosted service that owns the engine's optional parent-process
/// watchdog per <c>design § Engine options &gt; --parent-pid</c>
/// and <c>design § Lifecycle</c>: when
/// <see cref="EngineOptions.ParentProcessId"/> is set, the engine
/// watches that OS process and self-exits cleanly when it
/// vanishes. The watchdog clamps engine lifetime to the spawner's
/// lifetime, which is the only way to bound a long-running engine
/// after <c>--idle-timeout 0</c> disarms the idle gate.
/// </summary>
/// <remarks>
/// <para>
/// When <see cref="EngineOptions.ParentProcessId"/> is
/// <see langword="null"/> the watchdog is a no-op:
/// <see cref="StartAsync"/> logs the disabled state and returns
/// without opening any handles. With a value, the watchdog opens
/// the parent process at start, captures its
/// <see cref="IProcessHandle.StartTimeUtc"/> for diagnostics,
/// and awaits <see cref="IProcessHandle.WaitForExitAsync"/> on a
/// background task. The wait runs against the captured handle —
/// not against a freshly-resolved pid — so a later pid recycle
/// cannot fool the watchdog into thinking a new tenant is still
/// the original parent.
/// </para>
/// <para>
/// If the parent is already gone at <see cref="StartAsync"/>
/// (lookup returns <see langword="null"/>) the watchdog fires
/// immediately. The engine was spawned in a window where its
/// spawner had already exited; per the design's "clamp engine
/// lifetime to spawner lifetime" rule, the correct action is to
/// terminate rather than hang.
/// </para>
/// <para>
/// Firing means calling
/// <see cref="IHostApplicationLifetime.StopApplication"/>: the
/// same path SIGTERM and the idle gate take. The shutdown
/// sequence (emit <c>shutting-down</c> on <c>events</c>, drain
/// <c>rpc</c>, run the housekeeping sweep) is owned by
/// <see cref="Endpoints.EndpointHostService"/>; this watchdog only signals.
/// </para>
/// </remarks>
internal sealed partial class HostWatchdog : IHostedService, IAsyncDisposable
{
    private readonly IHostApplicationLifetime _applicationLifetime;
    private int _disposed;
    private readonly bool _enabled;
    private readonly ILogger<HostWatchdog> _logger;
    private readonly int _parentPid;
    private readonly IProcessLookup _processLookup;
    private int _started;
    private IProcessHandle? _parentHandle;
    private CancellationTokenSource? _waitCts;
    private Task? _waitTask;

    /// <summary>
    /// Creates a new <see cref="HostWatchdog"/>.
    /// </summary>
    /// <param name="options">Engine options carrying the resolved
    /// <see cref="EngineOptions.ParentProcessId"/>; a
    /// <see langword="null"/> value disables the watchdog.</param>
    /// <param name="applicationLifetime">Host lifetime the watchdog
    /// signals via <see cref="IHostApplicationLifetime.StopApplication"/>
    /// when the parent process exits.</param>
    /// <param name="processLookup">Abstraction over
    /// <see cref="System.Diagnostics.Process.GetProcessById(int)"/>
    /// — injectable so tests can drive the watchdog without
    /// spawning real processes.</param>
    /// <param name="logger">Diagnostic sink for arm/fire
    /// transitions in the <c>engine.lifecycle</c> log category.</param>
    /// <exception cref="ArgumentNullException">
    /// Any argument is <see langword="null"/>.
    /// </exception>
    public HostWatchdog(
        IOptions<EngineOptions> options,
        IHostApplicationLifetime applicationLifetime,
        IProcessLookup processLookup,
        ILogger<HostWatchdog> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(applicationLifetime);
        ArgumentNullException.ThrowIfNull(processLookup);
        ArgumentNullException.ThrowIfNull(logger);

        _applicationLifetime = applicationLifetime;
        _processLookup = processLookup;
        _logger = logger;
        var pid = options.Value.ParentProcessId;
        _enabled = pid is > 0;
        _parentPid = pid ?? 0;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await StopAsync(CancellationToken.None).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            throw new InvalidOperationException(
                $"{nameof(HostWatchdog)}.{nameof(StartAsync)} was called more than once; " +
                "hosted services are started exactly once by the generic host.");
        }

        if (!_enabled)
        {
            LogWatchdogDisabled(_logger);
            return Task.CompletedTask;
        }

        var handle = _processLookup.TryOpen(_parentPid);
        if (handle is null)
        {
            LogParentNotFoundAtStartup(_logger, _parentPid);
            _applicationLifetime.StopApplication();
            return Task.CompletedTask;
        }

        _parentHandle = handle;
        _waitCts = new CancellationTokenSource();
        LogWatchdogArmed(_logger, _parentPid, handle.StartTimeUtc);
        _waitTask = WaitForParentExitAsync(handle, _parentPid, _waitCts.Token);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var cts = _waitCts;
        _waitCts = null;
        if (cts is not null)
        {
            await cts.CancelAsync().ConfigureAwait(false);
        }

        var task = _waitTask;
        _waitTask = null;
        if (task is not null)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when StopAsync cancels the wait.
            }
        }

        cts?.Dispose();

        var handle = _parentHandle;
        _parentHandle = null;
        handle?.Dispose();
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Parent-process watchdog disabled (--parent-pid unset); engine has no opinion about its spawner.")]
    private static partial void LogWatchdogDisabled(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "Parent-process watchdog armed; watching pid {ParentPid} (started at {ParentStartTimeUtc:O}). Engine will exit when this process vanishes.")]
    private static partial void LogWatchdogArmed(ILogger logger, int parentPid, DateTime parentStartTimeUtc);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning,
        Message = "Parent-process watchdog fired at startup; pid {ParentPid} does not resolve to a live process. Requesting host shutdown.")]
    private static partial void LogParentNotFoundAtStartup(ILogger logger, int parentPid);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information,
        Message = "Parent-process watchdog fired; pid {ParentPid} exited. Requesting host shutdown.")]
    private static partial void LogParentExited(ILogger logger, int parentPid);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning,
        Message = "Parent-process watchdog observed an unexpected wait error for pid {ParentPid}; treating as parent gone and requesting host shutdown.")]
    private static partial void LogParentWaitFailed(ILogger logger, int parentPid, Exception exception);

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Clamp-to-spawner intent: any unexpected wait failure on an untrusted handle is treated as parent gone so the engine exits rather than hangs.")]
    private async Task WaitForParentExitAsync(
        IProcessHandle handle,
        int parentPid,
        CancellationToken cancellationToken)
    {
        try
        {
            await handle.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Host is stopping for an unrelated reason; do not fire.
            return;
        }
        catch (Exception ex)
        {
            // Any other wait failure is treated as "parent gone"
            // per the design's clamp-to-spawner intent — better to
            // exit than to hang on a watchdog we cannot trust.
            LogParentWaitFailed(_logger, parentPid, ex);
            _applicationLifetime.StopApplication();
            return;
        }

        LogParentExited(_logger, parentPid);
        _applicationLifetime.StopApplication();
    }
}
