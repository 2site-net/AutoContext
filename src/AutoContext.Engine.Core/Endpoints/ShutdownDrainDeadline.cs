namespace AutoContext.Engine.Core.Endpoints;

/// <summary>
/// Host-owned shutdown-drain deadline shared with the endpoint writer
/// pumps (the <c>events</c> and <c>logs</c> subscription loops). The
/// host drives its lifecycle — <see cref="Reset"/> at startup,
/// <see cref="StartDeadlineAsync"/> during graceful stop, and
/// <see cref="Release"/> once the pumps have drained — while the pumps
/// observe <see cref="Token"/> so a peer that stops reading
/// mid-shutdown cannot wedge teardown.
/// </summary>
/// <remarks>
/// <para>
/// The writer pumps deliberately do <em>not</em> observe the
/// accept-loop stop token: cancelling that token would tear the
/// connection down before the terminal <c>shutting-down</c> frame
/// reached the wire. Instead, <see cref="EndpointHostService.StopAsync"/>
/// publishes the terminal frame, then arms this drain deadline, and
/// only afterwards cancels the accept-loop stop token. A peer that
/// reads the frame before the deadline completes its pump naturally;
/// a peer that does not has its pending write cancelled once the
/// deadline fires, bounding teardown.
/// </para>
/// <para>
/// The three control methods — <see cref="Reset"/>,
/// <see cref="StartDeadlineAsync"/>, and <see cref="Release"/> — run
/// on the single host start/stop path and are never concurrent with
/// one another. The only cross-thread access is a pump reading
/// <see cref="Token"/> while <see cref="StartDeadlineAsync"/> arms the
/// underlying source; both operate on the same thread-safe
/// <see cref="CancellationTokenSource"/>. Each pump captures
/// <see cref="Token"/> once at entry, and the host awaits every
/// in-flight pump before calling <see cref="Release"/>, so the source
/// is never disposed out from under an observer.
/// </para>
/// </remarks>
internal sealed class ShutdownDrainDeadline
{
    private CancellationTokenSource? _cts;

    /// <summary>
    /// The drain-deadline token the writer pumps observe. Returns
    /// <see cref="CancellationToken.None"/> until <see cref="Reset"/>
    /// starts a cycle and after <see cref="Release"/> ends it.
    /// </summary>
    public CancellationToken Token
        => _cts?.Token ?? CancellationToken.None;

    /// <summary>
    /// Arms the drain deadline. A <paramref name="timeout"/> of zero
    /// or less cancels immediately; otherwise the deadline fires
    /// after the elapsed interval. Invoked by the host during
    /// graceful stop after the terminal frame has been published.
    /// </summary>
    /// <param name="timeout">Grace period peers have to drain the
    /// terminal frame before their pending write is cancelled.</param>
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
    /// Starts a fresh drain cycle, replacing any previous one. Invoked
    /// by the host at startup before any connection is accepted.
    /// </summary>
    public void Reset()
    {
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
    }

    /// <summary>
    /// Ends the drain cycle and releases the underlying source.
    /// Invoked by the host once every in-flight pump has drained.
    /// Idempotent.
    /// </summary>
    public void Release()
    {
        _cts?.Dispose();
        _cts = null;
    }
}
