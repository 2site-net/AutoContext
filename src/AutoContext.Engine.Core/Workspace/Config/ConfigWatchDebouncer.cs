namespace AutoContext.Engine.Core.Workspace.Config;

using System.Threading.Channels;

/// <summary>
/// Collapses a burst of raw signals into a single deferred callback
/// using a trailing-edge debounce: the callback fires once the signal
/// stream has stayed quiet for the configured window. Each
/// <see cref="Signal"/> during an open window pushes the deadline back,
/// so a flurry of filesystem events from one save reconciles exactly
/// once.
/// </summary>
/// <remarks>
/// <para>
/// Signals are funnelled through a capacity-one channel that drops
/// writes when full, so any number of overlapping signals coalesce into
/// at most one pending wake-up. A single long-lived consumer loop,
/// started by <see cref="Start"/>, drains the channel and runs the
/// quiet-window wait entirely on <see langword="await"/> continuations —
/// it holds no thread while idle.
/// </para>
/// <para>
/// The quiet window is scheduled through the supplied
/// <see cref="TimeProvider"/>, so tests drive it with a fake clock
/// instead of wall-clock sleeps. The callback is expected to handle its
/// own exceptions; an exception that escapes it faults the consumer loop
/// and surfaces from <see cref="Dispose"/>.
/// </para>
/// </remarks>
internal sealed class ConfigWatchDebouncer : IDisposable
{
    private CancellationTokenSource? _cts;
    private readonly TimeSpan _delay;
    private bool _disposed;
    private Task? _loop;
    private readonly Func<CancellationToken, Task> _onElapsed;
    private readonly Channel<byte> _signals = Channel.CreateBounded<byte>(
        new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false,
        });
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates a debouncer that invokes <paramref name="onElapsed"/>
    /// once each quiet window of <paramref name="delay"/> elapses.
    /// </summary>
    /// <param name="onElapsed">Callback run once per settled burst.
    /// Must handle its own exceptions.</param>
    /// <param name="timeProvider">Clock that schedules the quiet
    /// window.</param>
    /// <param name="delay">Quiet window to wait for after the last
    /// signal. Must be positive.</param>
    /// <exception cref="ArgumentNullException"><paramref name="onElapsed"/>
    /// or <paramref name="timeProvider"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="delay"/> is zero or negative.</exception>
    public ConfigWatchDebouncer(
        Func<CancellationToken, Task> onElapsed,
        TimeProvider timeProvider,
        TimeSpan delay)
    {
        ArgumentNullException.ThrowIfNull(onElapsed);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(delay, TimeSpan.Zero);

        _onElapsed = onElapsed;
        _timeProvider = timeProvider;
        _delay = delay;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts?.Cancel();

        try
        {
            _loop?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // Expected when the consumer loop unwinds on cancellation.
        }

        _cts?.Dispose();
    }

    /// <summary>
    /// Records a signal, (re)opening the quiet window. Cheap and
    /// non-blocking; safe to call from any thread, including a
    /// filesystem-watcher callback.
    /// </summary>
    public void Signal()
        => _signals.Writer.TryWrite(0);

    /// <summary>
    /// Starts the consumer loop. Idempotent; later calls are no-ops
    /// while the loop is running.
    /// </summary>
    public void Start()
    {
        if (_loop is not null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _loop = ConsumeAsync(_cts.Token);
    }

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        var reader = _signals.Reader;

        try
        {
            while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (reader.TryRead(out _))
                {
                }

                if (await QuietAsync(reader, cancellationToken).ConfigureAwait(false))
                {
                    await _onElapsed(cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Dispose requested; unwind the loop quietly.
        }
    }

    private async Task<bool> QuietAsync(ChannelReader<byte> reader, CancellationToken cancellationToken)
    {
        while (true)
        {
            using var quiet = new CancellationTokenSource(_delay, _timeProvider);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                quiet.Token, cancellationToken);

            try
            {
                if (!await reader.WaitToReadAsync(linked.Token).ConfigureAwait(false))
                {
                    return false;
                }

                while (reader.TryRead(out _))
                {
                }
            }
            catch (OperationCanceledException)
                when (quiet.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                return true;
            }
        }
    }
}
