namespace AutoContext.Engine.Core.Tests.Support.Workers;

using AutoContext.Engine.Core.Infrastructure.Diagnostics;
using AutoContext.Engine.Core.Workers;

/// <summary>
/// A single recorded launch from <see cref="FakeWorkerProcessLauncher"/>.
/// Exposes helpers that drive the captured
/// <see cref="IProcessObserver"/> to simulate the spawned worker's
/// stderr stream and exit, plus a <see cref="MarkReady"/> signal that
/// stands in for the worker's pipe becoming connectable.
/// </summary>
internal sealed class FakeWorkerLaunch(
    WorkerProcessInfo processInfo,
    IProcessObserver observer,
    FakeWorkerProcess process)
{
    public FakeWorkerProcess Process { get; } = process;

    public WorkerProcessInfo ProcessInfo { get; } = processInfo;

    /// <summary>
    /// Completed by <see cref="MarkReady"/>; the fake readiness probe
    /// resolves this launch's pipe against it.
    /// </summary>
    public TaskCompletionSource ReadySource { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void EmitStandardErrorLine(string line) => observer.OnStandardErrorLine(line);

    /// <summary>Signals that this worker's pipe is now connectable.</summary>
    public void MarkReady() => ReadySource.TrySetResult();

    public void EmitExit(int? exitCode) => observer.OnExited(exitCode);
}
