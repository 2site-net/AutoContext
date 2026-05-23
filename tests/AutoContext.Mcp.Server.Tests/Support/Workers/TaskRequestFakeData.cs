namespace AutoContext.Mcp.Server.Tests.Support.Workers;

using System.Collections.Frozen;
using System.Text.Json;

using AutoContext.Mcp.Server.Workers.Protocol;

/// <summary>
/// Canonical fake <see cref="TaskRequest"/> payloads shared across the
/// worker pipe-client tests.
/// </summary>
internal static class TaskRequestFakeData
{
    /// <summary>
    /// Builds a minimal valid <see cref="TaskRequest"/> with a
    /// <c>{ content = "hello" }</c> payload and an empty
    /// EditorConfig — sufficient to drive worker happy-path tests.
    /// </summary>
    public static TaskRequest BuildRequest(string mcpTask) => new()
    {
        McpTask = mcpTask,
        Data = JsonSerializer.SerializeToElement(new { content = "hello" }),
        EditorConfig = FrozenDictionary<string, string>.Empty,
        CorrelationId = "corr-test",
    };
}
