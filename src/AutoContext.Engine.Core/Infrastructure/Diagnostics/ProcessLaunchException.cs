namespace AutoContext.Engine.Core.Infrastructure.Diagnostics;

/// <summary>
/// Thrown when a process cannot be started, or when a launched process
/// exits prematurely. Carries the launch specification the failure
/// relates to so callers can include it in an actionable diagnostic.
/// </summary>
/// <typeparam name="T">The launch-specification type the failure relates to.</typeparam>
public sealed class ProcessLaunchException<T> : Exception
    where T : ProcessInfo
{
    /// <summary>
    /// Creates a new <see cref="ProcessLaunchException{T}"/>.
    /// </summary>
    /// <param name="processInfo">The specification the failure relates to.</param>
    /// <param name="message">The failure message.</param>
    public ProcessLaunchException(T processInfo, string message)
        : base(message)
    {
        ProcessInfo = processInfo;
    }

    /// <summary>
    /// Creates a new <see cref="ProcessLaunchException{T}"/> wrapping the
    /// underlying OS failure.
    /// </summary>
    /// <param name="processInfo">The specification the failure relates to.</param>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The underlying OS exception.</param>
    public ProcessLaunchException(T processInfo, string message, Exception innerException)
        : base(message, innerException)
    {
        ProcessInfo = processInfo;
    }

    /// <summary>The specification the failure relates to.</summary>
    public T ProcessInfo { get; }
}
