namespace AutoContext.Engine.Core.Tests.Support.Workers;

using AutoContext.Engine.Core.Infrastructure.Diagnostics;
using AutoContext.Engine.Core.Workers;

/// <summary>
/// In-memory <see cref="IProcessLauncher{T}"/> for driving
/// <see cref="WorkerManager"/> tests without spawning real processes.
/// Each launch is recorded as a <see cref="FakeWorkerLaunch"/> whose
/// observer the test drives directly to emit stderr lines or signal
/// exit. Set <see cref="FailWith"/> to make the next launch throw.
/// </summary>
internal sealed class FakeWorkerProcessLauncher : IProcessLauncher<WorkerProcessInfo>
{
    private readonly List<FakeWorkerLaunch> _launches = [];

    public IReadOnlyList<FakeWorkerLaunch> Launches => _launches;

    public int LaunchCount => _launches.Count;

    public ProcessLaunchException<WorkerProcessInfo>? FailWith { get; set; }

    public IProcess Launch(WorkerProcessInfo processInfo, IProcessObserver observer)
    {
        if (FailWith is { } failure)
        {
            throw failure;
        }

        var process = new FakeWorkerProcess(_launches.Count + 1);
        _launches.Add(new FakeWorkerLaunch(processInfo, observer, process));

        return process;
    }

    /// <summary>
    /// Returns the readiness source of the most recently recorded launch
    /// whose pipe matches <paramref name="pipeName"/>, or
    /// <see langword="null"/> when no launch targets that pipe. Used by
    /// <see cref="FakeWorkerConnectionProbe"/> to resolve readiness.
    /// </summary>
    public TaskCompletionSource? LatestReadySource(string pipeName)
    {
        for (var i = _launches.Count - 1; i >= 0; i--)
        {
            if (string.Equals(_launches[i].ProcessInfo.Endpoint, pipeName, StringComparison.Ordinal))
            {
                return _launches[i].ReadySource;
            }
        }

        return null;
    }
}
