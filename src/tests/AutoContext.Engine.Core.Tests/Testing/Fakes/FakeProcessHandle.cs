namespace AutoContext.Engine.Core.Tests.Testing.Fakes;

using AutoContext.Engine.Core.Infrastructure.Diagnostics;

/// <summary>
/// In-memory <see cref="IProcessHandle"/> used by
/// <see cref="HostWatchdog"/> tests. Exposes a
/// <see cref="SignalExit"/> hook the test calls to release any
/// in-flight <see cref="WaitForExitAsync"/> wait — modelling "the
/// parent process just died" without spawning a real OS process.
/// </summary>
internal sealed class FakeProcessHandle : IProcessHandle
{
    private readonly TaskCompletionSource _exitTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _disposed;

    public FakeProcessHandle(DateTime startTimeUtc)
    {
        StartTimeUtc = startTimeUtc;
    }

    public int DisposeCallCount => Volatile.Read(ref _disposed);

    /// <inheritdoc/>
    public DateTime StartTimeUtc { get; }

    /// <summary>
    /// Releases any pending <see cref="WaitForExitAsync"/> wait so
    /// the watchdog observes "parent exited". Idempotent.
    /// </summary>
    public void SignalExit() => _exitTcs.TrySetResult();

    /// <summary>
    /// Releases any pending <see cref="WaitForExitAsync"/> wait
    /// with an exception so the watchdog hits its fault path.
    /// Idempotent.
    /// </summary>
    public void SignalWaitFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        _exitTcs.TrySetException(exception);
    }

    /// <inheritdoc/>
    public void Dispose() => Interlocked.Increment(ref _disposed);

    /// <inheritdoc/>
    public Task WaitForExitAsync(CancellationToken cancellationToken) =>
        _exitTcs.Task.WaitAsync(cancellationToken);
}
