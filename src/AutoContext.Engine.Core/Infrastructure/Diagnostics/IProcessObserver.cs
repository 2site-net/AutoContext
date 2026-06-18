namespace AutoContext.Engine.Core.Infrastructure.Diagnostics;

/// <summary>
/// Sink a caller supplies to a launched process so it can observe the
/// process's stderr stream and its exit without the launcher knowing the
/// caller's internals. Implementations route every stderr line and the
/// single terminal exit through these callbacks.
/// </summary>
internal interface IProcessObserver
{
    /// <summary>
    /// Invoked exactly once when the process exits.
    /// </summary>
    /// <param name="exitCode">The process exit code, or
    /// <see langword="null"/> when it could not be read.</param>
    void OnExited(int? exitCode);

    /// <summary>
    /// Invoked once for each line the process writes to stderr, in
    /// arrival order. Callers inspect these lines for whatever
    /// process-specific signals they track.
    /// </summary>
    /// <param name="line">The stderr line, without its trailing newline.</param>
    void OnStandardErrorLine(string line);
}
