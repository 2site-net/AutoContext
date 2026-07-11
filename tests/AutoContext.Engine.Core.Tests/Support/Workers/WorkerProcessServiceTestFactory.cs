namespace AutoContext.Engine.Core.Tests.Support.Workers;

using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Workers;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Shared construction for <see cref="WorkerProcessService"/> tests: builds a
/// service registered with a single <c>dotnet</c> worker over the supplied
/// launcher and starts it so its worker hosts are populated.
/// </summary>
internal static class WorkerProcessServiceTestFactory
{
    public static WorkerProcessService Create(
        FakeWorkerProcessLauncher launcher,
        LogChannel? logChannel = null,
        TimeProvider? timeProvider = null)
    {
        var service = new WorkerProcessService(
            () => [WorkerProcessInfoFakeData.CreateValid("dotnet")],
            launcher,
            new FakeWorkerConnectionProbe(launcher),
            logChannel ?? new LogChannel(),
            timeProvider ?? TimeProvider.System,
            NullLogger<WorkerProcessService>.Instance);

        service.StartAsync(CancellationToken.None).GetAwaiter().GetResult();

        return service;
    }
}
