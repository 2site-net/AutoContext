namespace AutoContext.Engine.Core.Workers;

using AutoContext.Engine.Core.Infrastructure.Diagnostics;

/// <summary>
/// Production <see cref="IProcessLauncher{T}"/> that starts worker
/// processes through <see cref="System.Diagnostics.Process"/>. Disposes
/// the half-built process handle if the start fails so a failed launch
/// leaks no OS resources.
/// </summary>
internal sealed class WorkerProcessLauncher : IProcessLauncher<WorkerProcessInfo>
{
    /// <inheritdoc/>
    public IProcess Launch(WorkerProcessInfo processInfo, IProcessObserver observer)
    {
        ArgumentNullException.ThrowIfNull(processInfo);
        ArgumentNullException.ThrowIfNull(observer);

        var process = new WorkerProcess(processInfo, observer);

        try
        {
            process.Start();
        }
        catch (ProcessLaunchException<WorkerProcessInfo>)
        {
            process.Dispose();
            throw;
        }

        return process;
    }
}
