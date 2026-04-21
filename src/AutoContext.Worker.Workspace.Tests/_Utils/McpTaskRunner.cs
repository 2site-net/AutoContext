namespace AutoContext.Worker.Workspace.Tests._Utils;

using System.Text.Json;

using AutoContext.Mcp.Abstractions;

/// <summary>
/// Test helpers for invoking <see cref="IMcpTask"/> implementations with a JSON payload.
/// </summary>
internal static class McpTaskRunner
{
    public static Task<JsonElement> RunAsync(IMcpTask task, object data)
    {
        var element = JsonSerializer.SerializeToElement(data);

        return task.ExecuteAsync(element, TestContext.Current.CancellationToken);
    }
}
