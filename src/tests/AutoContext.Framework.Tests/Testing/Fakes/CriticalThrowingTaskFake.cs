namespace AutoContext.Framework.Tests.Testing.Fakes;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using AutoContext.Mcp;

/// <summary>
/// Throws a "critical" CLR exception (one that <c>WorkerTaskDispatcherService</c>
/// must let escape rather than convert into an error envelope).
/// </summary>
internal sealed class CriticalThrowingTaskFake : IMcpTask
{
    public string TaskName => "critical_boom";

    [SuppressMessage("Usage", "CA2201",
        Justification = "Test fixture intentionally throws a runtime-reserved exception to verify the dispatcher's critical-exception filter.")]
    public Task<JsonElement> ExecuteAsync(JsonElement data, CancellationToken ct) =>
        throw new OutOfMemoryException("simulated OOM");
}
