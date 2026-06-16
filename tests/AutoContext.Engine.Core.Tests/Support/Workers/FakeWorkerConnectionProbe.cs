namespace AutoContext.Engine.Core.Tests.Support.Workers;

using AutoContext.Engine.Core.Workers;

/// <summary>
/// In-memory <see cref="IWorkerConnectionProbe"/> for
/// <see cref="WorkerManager"/> tests. Resolves a worker's readiness
/// against the matching <see cref="FakeWorkerLaunch.ReadySource"/> on the
/// supplied launcher, so a test signals readiness via
/// <see cref="FakeWorkerLaunch.MarkReady"/> rather than dialling a real
/// pipe.
/// </summary>
internal sealed class FakeWorkerConnectionProbe(FakeWorkerProcessLauncher launcher) : IWorkerConnectionProbe
{
    /// <inheritdoc/>
    public Task WaitForConnectionAsync(string endpoint, CancellationToken cancellationToken)
    {
        var source = launcher.LatestReadySource(endpoint)
            ?? throw new InvalidOperationException(
                $"No fake launch registered for pipe '{endpoint}'.");

        return source.Task.WaitAsync(cancellationToken);
    }
}
