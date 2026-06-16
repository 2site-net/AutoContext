namespace AutoContext.Engine.Core.Tests.Support.Workers;

using AutoContext.Engine.Core.Workers;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Shared construction for <see cref="WorkerManager"/> tests: builds a
/// manager registered with a single <c>dotnet</c> worker over the
/// supplied launcher.
/// </summary>
internal static class WorkerManagerTestFactory
{
    public static WorkerManager Create(FakeWorkerProcessLauncher launcher) =>
        new(
            [WorkerProcessInfoFakeData.CreateValid("dotnet")],
            launcher,
            new FakeWorkerConnectionProbe(launcher),
            NullLogger<WorkerManager>.Instance);
}
