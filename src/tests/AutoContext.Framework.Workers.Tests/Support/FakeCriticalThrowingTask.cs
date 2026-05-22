namespace AutoContext.Framework.Workers.Tests.Support;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using AutoContext.Framework.Workers;

/// <summary>
/// Throws a "critical" CLR exception (one that <c>WorkerTaskDispatcherService</c>
/// must let escape rather than convert into an error envelope).
/// </summary>
internal sealed class FakeCriticalThrowingTask : IMcpTask
{
    public string TaskName => "critical_boom";

    [SuppressMessage("Usage", "CA2201",
        Justification = "Test fixture intentionally throws a runtime-reserved exception to verify the dispatcher's critical-exception filter.")]
    public Task<JsonElement> ExecuteAsync(JsonElement data, CancellationToken cancellationToken) =>
        throw new OutOfMemoryException("simulated OOM");
}
