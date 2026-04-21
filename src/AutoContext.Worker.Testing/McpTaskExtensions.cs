namespace AutoContext.Worker.Testing;

using System.Text.Json;

using AutoContext.Mcp;

/// <summary>
/// Test-only extensions over <see cref="IMcpTask"/> that let test code invoke
/// a task with an arbitrary anonymous-object payload and get back the raw
/// <see cref="JsonElement"/> the task produced. Shared across every
/// <c>AutoContext.Worker.*.Tests</c> project so the JSON-serialisation glue
/// and <see cref="TestContext.Current"/> cancellation-token plumbing live in
/// one place.
/// </summary>
public static class McpTaskExtensions
{
    /// <summary>
    /// Serialises <paramref name="data"/> to a <see cref="JsonElement"/> and
    /// invokes <paramref name="task"/> with the current xUnit test
    /// cancellation token.
    /// </summary>
    public static Task<JsonElement> ExecuteAsync(this IMcpTask task, object data)
    {
        ArgumentNullException.ThrowIfNull(task);

        var element = JsonSerializer.SerializeToElement(data);

        return task.ExecuteAsync(element, TestContext.Current.CancellationToken);
    }
}
