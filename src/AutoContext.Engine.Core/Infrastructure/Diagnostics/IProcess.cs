namespace AutoContext.Engine.Core.Infrastructure.Diagnostics;

/// <summary>
/// Handle to a launched child process. Callers use it to identify the
/// process for diagnostics and to terminate it; the process routes its
/// stderr lines and exit notifications through the
/// <see cref="IProcessObserver"/> supplied at launch.
/// </summary>
internal interface IProcess : IDisposable
{
    /// <summary>
    /// The OS process id, or <see langword="null"/> when it is not
    /// available (the process never started or has already been reaped).
    /// </summary>
    int? ProcessId { get; }

    /// <summary>
    /// Terminates the process and its child tree. Safe to call when the
    /// process has already exited.
    /// </summary>
    void Kill();
}
