namespace AutoContext.Mcp.Server.Tests.Tools.Invocation;

using System.Text.Json;

using AutoContext.Mcp.Server.EditorConfig;
using AutoContext.Mcp.Server.Registry;
using AutoContext.Mcp.Server.Tests.Testing.Utils;
using AutoContext.Mcp.Server.Tools.Invocation;
using AutoContext.Mcp.Server.Tools.Results;
using AutoContext.Mcp.Server.Workers;
using AutoContext.Mcp.Server.Workers.Protocol;

public sealed class ToolDelegateFactoryTests
{
    [Fact]
    public void Should_build_delegate_per_tool_keyed_by_name()
    {
        // Arrange
        var registry = BuildCatalog(
            ("AutoContext.Worker.Alpha", "autocontext.worker-alpha", [BuildTool("tool_one", "task_one")]),
            ("AutoContext.Worker.Beta", "autocontext.worker-beta", [BuildTool("tool_two", "task_two")]));
        var invoker = BuildInvoker();

        // Act
        var delegates = ToolDelegateFactory.Build(registry, invoker);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(2, delegates.Count),
            () => Assert.Contains("tool_one", delegates.Keys),
            () => Assert.Contains("tool_two", delegates.Keys));
    }

    [Fact]
    public void Should_throw_on_duplicate_tool_names()
    {
        // Arrange
        var registry = BuildCatalog(
            ("AutoContext.Worker.Alpha", "autocontext.worker-alpha", [BuildTool("dup_tool", "task_a")]),
            ("AutoContext.Worker.Beta", "autocontext.worker-beta", [BuildTool("dup_tool", "task_b")]));
        var invoker = BuildInvoker();

        // Act + Assert
        Assert.Throws<InvalidOperationException>(() => ToolDelegateFactory.Build(registry, invoker));
    }

    [Fact]
    public async Task Should_return_serialized_envelope_json_when_delegate_invoked()
    {
        // Arrange
        var endpoint = PipeServerHarness.UniqueEndpoint();
        var registry = BuildCatalog(
            ("AutoContext.Worker.Alpha", endpoint, [BuildTool("invoke_tool", "task_x")]));
        var pipeClient = new WorkerClient(TimeSpan.FromSeconds(5));
        var batcher = new EditorConfigBatcher(pipeClient, "autocontext-test-workspace-unused");
        var invoker = new ToolInvoker(pipeClient, batcher);

        var serverTask = PipeServerHarness.RunOneShotAsync(
            endpoint,
            handler: requestBytes =>
            {
                var request = JsonSerializer.Deserialize<TaskRequest>(
                    requestBytes,
                    WorkerJsonOptions.Instance)!;

                var response = new TaskResponse
                {
                    McpTask = request.McpTask,
                    Status = TaskResponse.StatusOk,
                    Output = JsonSerializer.SerializeToElement(new { ok = true }),
                    Error = string.Empty,
                };

                return JsonSerializer.SerializeToUtf8Bytes(response, WorkerJsonOptions.Instance);
            },
            ct: TestContext.Current.CancellationToken);

        var delegates = ToolDelegateFactory.Build(registry, invoker);
        var data = JsonSerializer.SerializeToElement(new { }, WorkerJsonOptions.Instance);

        // Act
        var handler = delegates["invoke_tool"];
        var json = await handler(data, TestContext.Current.CancellationToken);
        await serverTask;

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // Assert
        Assert.Multiple(
            () => Assert.Equal("invoke_tool", root.GetProperty("tool").GetString()),
            () => Assert.Equal(ToolResultEnvelope.StatusOk, root.GetProperty("status").GetString()),
            () => Assert.Equal(1, root.GetProperty("summary").GetProperty("taskCount").GetInt32()),
            () => Assert.Equal("task_x", root.GetProperty("result")[0].GetProperty("task").GetString()));
    }

    private static ToolInvoker BuildInvoker()
    {
        var pipeClient = new WorkerClient(TimeSpan.FromSeconds(5));
        var batcher = new EditorConfigBatcher(pipeClient, "autocontext-test-workspace-unused");
        return new ToolInvoker(pipeClient, batcher);
    }

    private static McpWorkersCatalog BuildCatalog(
        params (string Name, string Endpoint, IReadOnlyList<McpToolDefinition> Definitions)[] workers)
    {
        var list = new List<McpWorker>(workers.Length);

        foreach (var (name, endpoint, definitions) in workers)
        {
            list.Add(new McpWorker
            {
                Name = name,
                Endpoint = endpoint,
                Tools = definitions,
            });
        }

        return new McpWorkersCatalog
        {
            SchemaVersion = "1",
            Workers = list,
        };
    }

    private static McpToolDefinition BuildTool(string name, params string[] taskNames)
    {
        var tasks = new List<McpTaskDefinition>(taskNames.Length);

        foreach (var taskName in taskNames)
        {
            tasks.Add(new McpTaskDefinition { Name = taskName });
        }

        return new McpToolDefinition
        {
            Name = name,
            Description = "Test tool.",
            Parameters = new Dictionary<string, McpToolParameter>(StringComparer.Ordinal),
            Tasks = tasks,
        };
    }
}
