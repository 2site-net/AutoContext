namespace AutoContext.Engine.Core.Watchdogs;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Hosted service that owns the engine's idle-shutdown gate per
/// <c>design § Lifecycle &gt; Idle shutdown</c>: the engine exits
/// after <see cref="EngineOptions.IdleTimeout"/> seconds with no
/// keep-alive clients connected, with a fixed
/// <see cref="GracePeriod"/> grace window after the last
/// keep-alive disconnect to absorb host churn (VS Code extension
/// host reload, language-service refresh).
/// </summary>
/// <remarks>
/// <para>
/// Only <c>rpc</c> and <c>events</c> connections count toward the
/// gate; <c>health</c> and <c>logs</c> are passive observers and
/// must not pin the engine alive. The watchdog enforces this by
/// exposing a single counter surface
/// (<see cref="AcquireKeepAliveAsync"/>) — the caller decides which
/// connections register, and <see cref="Endpoints.EndpointHostService"/> only
/// registers <c>rpc</c> and <c>events</c>.
/// </para>
/// <para>
/// Setting <c>--idle-timeout 0</c> disables the gate entirely:
/// <see cref="AcquireKeepAliveAsync"/> returns a no-op token,
/// <see cref="StartAsync"/> logs the disabled state and returns
/// without arming a timer, and the engine then lives until an
/// external lifecycle clamp fires (the <c>Engine.Shutdown</c> RPC,
/// SIGINT / SIGTERM, the optional parent-pid watchdog).
/// </para>
/// <para>
/// At startup the timer arms immediately because no keep-alive
/// clients are yet connected. Per the design's "short-lived
/// spawners keep the default so a forgotten engine still cleans
/// up" rule, an engine nobody ever dials self-terminates.
/// </para>
/// </remarks>
internal sealed partial class IdleTimeoutWatchdog : IHostedService, IAsyncDisposable
{
    /// <summary>
    /// Fixed grace window added to <see cref="EngineOptions.IdleTimeout"/>
    /// before the gate fires after the last keep-alive disconnect.
    /// Sized per design to absorb VS Code reload churn without
    /// triggering a spurious exit.
    /// </summary>
    internal static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(2);

    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly TimeProvider _clock;
    private int _count;
    private int _disposed;
    private readonly bool _enabled;
    private readonly Lock _gate = new();
    private readonly TimeSpan _idleTimeout;
    private readonly ILogger<IdleTimeoutWatchdog> _logger;
    private bool _started;
    private CancellationTokenSource? _timerCts;

    /// <summary>
    /// Creates a new <see cref="IdleTimeoutWatchdog"/>.
    /// </summary>
    /// <param name="options">Engine options carrying the resolved
    /// <see cref="EngineOptions.IdleTimeout"/>.</param>
    /// <param name="applicationLifetime">Host lifetime the watchdog
    /// signals via <see cref="IHostApplicationLifetime.StopApplication"/>
    /// when the idle window elapses.</param>
    /// <param name="timeProvider">Clock used to schedule the
    /// countdown — injectable so tests can use a virtual clock.</param>
    /// <param name="logger">Diagnostic sink for arm/disarm/fire
    /// transitions in the <c>engine.lifecycle</c> log category.</param>
    /// <exception cref="ArgumentNullException">
    /// Any argument is <see langword="null"/>.
    /// </exception>
    public IdleTimeoutWatchdog(
        IOptions<EngineOptions> options,
        IHostApplicationLifetime applicationLifetime,
        TimeProvider timeProvider,
        ILogger<IdleTimeoutWatchdog> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(applicationLifetime);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _applicationLifetime = applicationLifetime;
        _clock = timeProvider;
        _logger = logger;
        _idleTimeout = options.Value.IdleTimeout;
        _enabled = _idleTimeout > TimeSpan.Zero;
    }

    /// <summary>
    /// Registers a keep-alive holder. While at least one token is
    /// outstanding the gate is disarmed; when the last token is
    /// disposed the gate re-arms. Returns a no-op token when the
    /// gate is disabled (<c>--idle-timeout 0</c>).
    /// </summary>
    /// <returns>A token whose async disposal releases the
    /// keep-alive hold. Disposal is idempotent and thread-safe.</returns>
    public async ValueTask<IAsyncDisposable> AcquireKeepAliveAsync()
    {
        if (!_enabled)
        {
            return NoopReleaser.Instance;
        }

        CancellationTokenSource? toCancel = null;
        int newCount;

        lock (_gate)
        {
            _count++;
            newCount = _count;
            if (_count == 1)
            {
                toCancel = _timerCts;
                _timerCts = null;
            }
        }

        if (toCancel is not null)
        {
            await toCancel.CancelAsync().ConfigureAwait(false);
            toCancel.Dispose();
            LogGateDisarmed(_logger, newCount);
        }

        return new KeepAliveToken(this);
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

        if (!_enabled)
        {
            LogGateDisabled(_logger);
            return Task.CompletedTask;
        }

        lock (_gate)
        {
            _started = true;
            // No keep-alive holders at startup → arm immediately so
            // an engine nobody dials self-terminates.
            ArmTimerLocked();
        }

        LogGateArmed(_logger, _idleTimeout, GracePeriod);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource? toCancel;

        lock (_gate)
        {
            _started = false;
            toCancel = _timerCts;
            _timerCts = null;
        }

        if (toCancel is not null)
        {
            await toCancel.CancelAsync().ConfigureAwait(false);
            toCancel.Dispose();
        }
    }

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "Idle-timeout gate armed; engine will exit after {IdleTimeout} of no rpc/events connections (plus {GracePeriod} grace).")]
    private static partial void LogGateArmed(ILogger logger, TimeSpan idleTimeout, TimeSpan gracePeriod);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Idle-timeout gate disabled (--idle-timeout 0); engine will run until stopped externally.")]
    private static partial void LogGateDisabled(ILogger logger);

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug,
        Message = "Idle-timeout gate disarmed; {KeepAliveCount} keep-alive holder(s) connected.")]
    private static partial void LogGateDisarmed(ILogger logger, int keepAliveCount);

    [LoggerMessage(EventId = 5, Level = LogLevel.Debug,
        Message = "Idle-timeout gate re-armed; last keep-alive holder disconnected.")]
    private static partial void LogGateReArmed(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "Idle-timeout fired after {IdleTimeout} of inactivity; requesting host shutdown.")]
    private static partial void LogIdleTimeoutFired(ILogger logger, TimeSpan idleTimeout);

    private void ArmTimerLocked()
    {
        var cts = new CancellationTokenSource();
        _timerCts = cts;
        // Fire-and-forget: the timer task observes its own
        // cancellation token and is the sole owner of the
        // post-delay action. Capture the local cts to avoid racing
        // with concurrent re-arms.
        _ = RunTimerAsync(_idleTimeout + GracePeriod, cts.Token);
    }

    private void Release()
    {
        if (!_enabled)
        {
            return;
        }

        bool reArmed = false;

        lock (_gate)
        {
            if (_count == 0)
            {
                // Defensive: tokens are idempotent on dispose so
                // this should not happen, but tolerate it.
                return;
            }

            _count--;
            if (_count == 0 && _started)
            {
                ArmTimerLocked();
                reArmed = true;
            }
        }

        if (reArmed)
        {
            LogGateReArmed(_logger);
        }
    }

    private async Task RunTimerAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, _clock, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        LogIdleTimeoutFired(_logger, _idleTimeout);
        _applicationLifetime.StopApplication();
    }

    private sealed class KeepAliveToken(IdleTimeoutWatchdog owner) : IAsyncDisposable
    {
        private int _disposed;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.Release();
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class NoopReleaser : IAsyncDisposable
    {
        public static readonly NoopReleaser Instance = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
