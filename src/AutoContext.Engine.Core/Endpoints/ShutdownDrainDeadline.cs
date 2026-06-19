namespace AutoContext.Engine.Core.Endpoints;

/// <summary>
/// Host-owned shutdown-drain deadline shared by the endpoint writer
/// pumps: the <c>events</c> and <c>logs</c> subscription loops.
/// </summary>
/// <remarks>
/// <para>
/// The deadline is inactive until <see cref="Reset"/> creates a fresh
/// token source for the current host run. During graceful stop,
/// <see cref="EndpointHostService.StopAsync"/> publishes the terminal
/// <c>shutting-down</c> frame, then calls
/// <see cref="StartDeadlineAsync"/> to schedule cancellation of
/// <see cref="Token"/>. Once all in-flight pumps have completed,
/// the host calls <see cref="Release"/> to dispose the source and
/// return the deadline to its inactive state.
/// </para>
/// <para>
/// Writer pumps deliberately do <em>not</em> observe the accept-loop
/// stop token once streaming begins: cancelling that token would tear
/// the connection down before the terminal frame reached the wire.
/// Instead, they capture <see cref="Token"/> and use it as the bounded
/// drain deadline. A peer that reads promptly completes naturally; a
/// peer that stops reading has its pending write cancelled when the
/// deadline expires.
/// </para>
/// <para>
/// The control methods — <see cref="Reset"/>,
/// <see cref="StartDeadlineAsync"/>, and <see cref="Release"/> — are
/// called only from the host start/stop path and are not concurrent
/// with one another. Pumps may read <see cref="Token"/> while
/// <see cref="StartDeadlineAsync"/> schedules cancellation, but both
/// operations are safe on the same <see cref="CancellationTokenSource"/>.
/// The host waits for every in-flight pump before calling
/// <see cref="Release"/>, so the source is not disposed while a pump is
/// still using its captured token.
/// </para>
/// </remarks>
internal sealed class ShutdownDrainDeadline
{
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Token observed by writer pumps while shutdown drain is active.
    /// Returns <see cref="CancellationToken.None"/> before
    /// <see cref="Reset"/> and after <see cref="Release"/>.
    /// </summary>
    public CancellationToken Token
        => _cts?.Token ?? CancellationToken.None;

    /// <summary>
    /// Starts the shutdown-drain deadline for the current token source.
    /// A <paramref name="timeout"/> of zero or less cancels
    /// <see cref="Token"/> immediately; otherwise the token is cancelled
    /// after the elapsed interval.
    /// </summary>
    /// <param name="timeout">
    /// Grace period for connected peers to read terminal frames before
    /// pending writes are cancelled.
    /// </param>
    public Task StartDeadlineAsync(TimeSpan timeout)
    {
        if (_cts is not { } cts)
        {
            return Task.CompletedTask;
        }

        if (timeout <= TimeSpan.Zero)
        {
            return cts.CancelAsync();
        }

        cts.CancelAfter(timeout);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates a fresh, non-cancelled deadline token source for the
    /// current host run, replacing any previous source.
    /// </summary>
    public void Reset()
    {
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
    }

    /// <summary>
    /// Releases the current deadline token source and returns the
    /// deadline to its inactive state. Idempotent.
    /// </summary>
    public void Release()
    {
        _cts?.Dispose();
        _cts = null;
    }
}
