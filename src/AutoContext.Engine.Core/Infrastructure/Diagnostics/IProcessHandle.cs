namespace AutoContext.Engine.Core.Infrastructure.Diagnostics;

/// <summary>
/// Thin abstraction over a live OS process handle. Hides
/// <see cref="System.Diagnostics.Process"/> behind a seam so
/// watchdog logic that depends on "wait for this process to exit"
/// is unit-testable without spawning real children.
/// </summary>
/// <remarks>
/// Implementations <i>own</i> the underlying handle and must
/// release it from <see cref="IDisposable.Dispose"/>. The
/// production implementation wraps
/// <see cref="System.Diagnostics.Process"/>; tests inject a fake
/// that completes <see cref="WaitForExitAsync"/> on demand.
/// </remarks>
internal interface IProcessHandle : IDisposable
{
    /// <summary>
    /// UTC start time of the process this handle refers to.
    /// Captured at handle-open time so pid recycling cannot defeat
    /// downstream identity checks (the same value the engine
    /// records into <c>engine-registry.json</c>'s
    /// <c>processStartTimeUtc</c>).
    /// </summary>
    DateTime StartTimeUtc { get; }

    /// <summary>
    /// Awaits process exit. Completes when the underlying process
    /// terminates for any reason. The task observes
    /// <paramref name="cancellationToken"/> so callers can stop
    /// waiting without leaking the wait.
    /// </summary>
    Task WaitForExitAsync(CancellationToken cancellationToken);
}
