namespace AutoContext.Engine.Core.Tests.Support.Workers;

using AutoContext.Engine.Core.Infrastructure.Diagnostics;
using AutoContext.Engine.Core.Workers;

/// <summary>
/// Fake <see cref="IProcess"/> that records whether it was killed
/// and disposed so tests can assert <see cref="WorkerProcessService"/> tears
/// down its processes.
/// </summary>
internal sealed class FakeWorkerProcess(int? processId) : IProcess
{
    public int? ProcessId { get; } = processId;

    public bool Killed { get; private set; }

    public bool Disposed { get; private set; }

    public void Kill() => Killed = true;

    public void Dispose() => Disposed = true;
}
