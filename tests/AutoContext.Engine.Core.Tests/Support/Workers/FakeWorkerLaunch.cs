namespace AutoContext.Engine.Core.Tests.Support.Workers;

using AutoContext.Engine.Core.Infrastructure.Diagnostics;
using AutoContext.Engine.Core.Workers;

/// <summary>
/// A single recorded launch from <see cref="FakeWorkerProcessLauncher"/>.
/// Exposes helpers that drive the captured
/// <see cref="IProcessObserver"/> to simulate the spawned worker's
/// stderr stream and exit.
/// </summary>
internal sealed class FakeWorkerLaunch(
    WorkerProcessInfo processInfo,
    IProcessObserver observer,
    FakeWorkerProcess process)
{
    public FakeWorkerProcess Process { get; } = process;

    public WorkerProcessInfo ProcessInfo { get; } = processInfo;

    public void EmitStandardErrorLine(string line) => observer.OnStandardErrorLine(line);

    public void EmitReadyMarker() => observer.OnStandardErrorLine(ProcessInfo.ReadyMarker);

    public void EmitExit(int? exitCode) => observer.OnExited(exitCode);
}
