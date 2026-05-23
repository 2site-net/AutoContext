namespace AutoContext.Framework.Workers.Tests.Support;

using AutoContext.Framework.Workers;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Builds <see cref="WorkerHealthMonitorService"/> instances with the
/// test project's default logger wiring.
/// </summary>
internal static class WorkerHealthMonitorServiceTestFactory
{
    public static WorkerHealthMonitorService Create(string pipeName, string clientId) =>
        new(pipeName, clientId, NullLogger<WorkerHealthMonitorService>.Instance);
}
