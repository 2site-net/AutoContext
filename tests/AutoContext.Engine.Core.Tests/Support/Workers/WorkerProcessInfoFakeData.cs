namespace AutoContext.Engine.Core.Tests.Support.Workers;

using AutoContext.Engine.Core.Workers;

/// <summary>
/// Builds valid <see cref="WorkerProcessInfo"/> values for tests. Tweak a
/// single field with a <c>with</c> expression over
/// <see cref="CreateValid"/>.
/// </summary>
internal static class WorkerProcessInfoFakeData
{
    public static WorkerProcessInfo CreateValid(string workerId = "dotnet") =>
        new()
        {
            WorkerId = workerId,
            Command = $"AutoContext.Worker.{workerId}",
            Arguments = ["--instance-id", "00000000-0000-0000-0000-000000000000"],
            Endpoint = $"autocontext.worker-{workerId}#test",
        };
}
