namespace AutoContext.Engine.Core.Infrastructure.Diagnostics;

/// <summary>
/// Opens <see cref="IProcessHandle"/> instances by OS pid. The
/// sole production implementation wraps
/// <see cref="System.Diagnostics.Process.GetProcessById(int)"/>;
/// tests substitute a fake to drive the parent-process watchdog
/// without spawning real processes.
/// </summary>
internal interface IProcessLookup
{
    /// <summary>
    /// Tries to open a handle to the process with id
    /// <paramref name="processId"/>. Returns <see langword="null"/>
    /// when no live process owns that id, when the caller lacks
    /// permission to query it, or when the OS denies access to its
    /// metadata. Callers treat a null return as "parent already
    /// gone".
    /// </summary>
    /// <param name="processId">OS process id to open.</param>
    IProcessHandle? TryOpen(int processId);
}
