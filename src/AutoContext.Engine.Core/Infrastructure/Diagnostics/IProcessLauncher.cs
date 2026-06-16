namespace AutoContext.Engine.Core.Infrastructure.Diagnostics;

/// <summary>
/// Seam over OS process creation so launch-dependent logic is
/// unit-testable without spawning real processes. Tests substitute an
/// in-process fake that drives the supplied <see cref="IProcessObserver"/>
/// directly.
/// </summary>
/// <typeparam name="T">The launch specification this launcher accepts.</typeparam>
internal interface IProcessLauncher<T>
    where T : ProcessInfo
{
    /// <summary>
    /// Starts the process described by <paramref name="processInfo"/>,
    /// routing its stderr lines and exit to <paramref name="observer"/>.
    /// </summary>
    /// <param name="processInfo">The resolved launch specification.</param>
    /// <param name="observer">The sink for the process's stderr and exit
    /// notifications.</param>
    /// <returns>A handle to the started process.</returns>
    /// <exception cref="ProcessLaunchException{T}">
    /// The process could not be started.</exception>
    IProcess Launch(T processInfo, IProcessObserver observer);
}
