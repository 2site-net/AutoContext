namespace AutoContext.Engine.Core.Tests.Support.Workers;

using AutoContext.Engine.Core.Workers;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Shared construction for <see cref="WorkerProcessService"/> tests: builds a
/// service registered with a single <c>dotnet</c> worker over the supplied
/// launcher and starts it so its worker hosts are populated.
/// </summary>
internal static class WorkerProcessServiceTestFactory
{
    public static WorkerProcessService Create(FakeWorkerProcessLauncher launcher)
    {
        var service = new WorkerProcessService(
            () => [WorkerProcessInfoFakeData.CreateValid("dotnet")],
            launcher,
            new FakeWorkerConnectionProbe(launcher),
            NullLogger<WorkerProcessService>.Instance);

        service.StartAsync(CancellationToken.None).GetAwaiter().GetResult();

        return service;
    }
}
