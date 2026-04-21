namespace AutoContext.Worker.Testing;

using System.Text.Json;

using AutoContext.Mcp;

/// <summary>
/// Test helpers for invoking <see cref="IMcpTask"/> implementations with a JSON payload.
/// Shared across every <c>AutoContext.Worker.*.Tests</c> project so each test
/// file can construct a task, hand it an arbitrary anonymous-object payload,
/// and get back the raw <see cref="JsonElement"/> the task produced.
/// </summary>
public static class McpTaskRunner
{
    public static Task<JsonElement> RunAsync(IMcpTask task, object data)
    {
        ArgumentNullException.ThrowIfNull(task);

        var element = JsonSerializer.SerializeToElement(data);

        return task.ExecuteAsync(element, TestContext.Current.CancellationToken);
    }
}
