namespace AutoContext.Framework.Workers.Tests.Support;

using AutoContext.Framework.Workers;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

internal static class WorkerTaskDispatcherServiceTestFactory
{
    public const string TestReadyMarker = "[AutoContext.Worker.Tests] Ready.";

    public static WorkerTaskDispatcherService CreateService(string pipeName, IMcpTask[] tasks)
    {
        var options = Options.Create(new WorkerHostOptions
        {
            Pipe = pipeName,
            ReadyMarker = TestReadyMarker,
        });

        return new WorkerTaskDispatcherService(options, tasks, NullLogger<WorkerTaskDispatcherService>.Instance);
    }
}
