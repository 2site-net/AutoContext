namespace AutoContext.Mcp.Server.Tests.Tools.Invocation;

using System.Text.Json;

using AutoContext.Mcp.Server.EditorConfig;
using AutoContext.Mcp.Server.Registry;
using AutoContext.Mcp.Server.Tests.Testing.Utils;
using AutoContext.Mcp.Server.Tools.Invocation;
using AutoContext.Mcp.Server.Tools.Results;
using AutoContext.Mcp.Server.Workers;
using AutoContext.Mcp.Server.Workers.Protocol;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class ToolDelegateFactoryTests
{
    [Fact]
    public void Should_build_delegate_per_tool_keyed_by_name()
    {
        // Arrange
        var registry = BuildCatalog(
            ("alpha", "AutoContext.Worker.Alpha", [BuildTool("tool_one", "task_one")]),
            ("beta", "AutoContext.Worker.Beta", [BuildTool("tool_two", "task_two")]));
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
            ("alpha", "AutoContext.Worker.Alpha", [BuildTool("dup_tool", "task_a")]),
            ("beta", "AutoContext.Worker.Beta", [BuildTool("dup_tool", "task_b")]));
        var invoker = BuildInvoker();

        // Act + Assert
        Assert.Throws<InvalidOperationException>(() => ToolDelegateFactory.Build(registry, invoker));
    }

    [Fact]
    public async Task Should_return_serialized_envelope_json_when_delegate_invoked()
    {
        // Arrange
        var workerId = PipeServerHarness.UniqueWorkerId();
        var pipeName = PipeServerHarness.PipeNameFor(workerId);
        var registry = BuildCatalog(
            (workerId, "AutoContext.Worker.Alpha", [BuildTool("invoke_tool", "task_x")]));
        var workerClient = new WorkerClient(TimeSpan.FromSeconds(5));
        var batcher = new EditorConfigBatcher(workerClient, "autocontext-test-workspace-unused", NullLogger<EditorConfigBatcher>.Instance);
        var invoker = new ToolInvoker(workerClient, batcher);

        var serverTask = PipeServerHarness.RunOneShotAsync(
            pipeName,
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
        var json = await handler(data, "corr-test", TestContext.Current.CancellationToken);
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

    [Fact]
    public async Task Should_route_each_delegate_to_its_own_worker_pipe()
    {
        // Regression: the per-tool delegate must capture and dispatch to
        // *its own* worker. A closure-over-loop-variable bug would make
        // every delegate route to the last-iterated worker's pipe, so
        // the alpha tool would surface beta's `servedBy` marker.
        // Arrange
        var alphaWorkerId = PipeServerHarness.UniqueWorkerId();
        var betaWorkerId = PipeServerHarness.UniqueWorkerId();
        var alphaPipeName = PipeServerHarness.PipeNameFor(alphaWorkerId);
        var betaPipeName = PipeServerHarness.PipeNameFor(betaWorkerId);

        var registry = BuildCatalog(
            (alphaWorkerId, "AutoContext.Worker.Alpha", [BuildTool("alpha_tool", "task_alpha")]),
            (betaWorkerId, "AutoContext.Worker.Beta", [BuildTool("beta_tool", "task_beta")]));

        var workerClient = new WorkerClient(TimeSpan.FromSeconds(5));
        var batcher = new EditorConfigBatcher(workerClient, "autocontext-test-workspace-unused", NullLogger<EditorConfigBatcher>.Instance);
        var invoker = new ToolInvoker(workerClient, batcher);

        var alphaServerTask = PipeServerHarness.RunOneShotAsync(
            alphaPipeName,
            handler: requestBytes => BuildMarkedResponse(requestBytes, servedBy: "alpha"),
            ct: TestContext.Current.CancellationToken);
        var betaServerTask = PipeServerHarness.RunOneShotAsync(
            betaPipeName,
            handler: requestBytes => BuildMarkedResponse(requestBytes, servedBy: "beta"),
            ct: TestContext.Current.CancellationToken);

        var delegates = ToolDelegateFactory.Build(registry, invoker);
        var data = JsonSerializer.SerializeToElement(new { }, WorkerJsonOptions.Instance);

        // Act
        var alphaJson = await delegates["alpha_tool"](data, "corr-test", TestContext.Current.CancellationToken);
        var betaJson = await delegates["beta_tool"](data, "corr-test", TestContext.Current.CancellationToken);
        await alphaServerTask;
        await betaServerTask;

        using var alphaDoc = JsonDocument.Parse(alphaJson);
        using var betaDoc = JsonDocument.Parse(betaJson);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("alpha", alphaDoc.RootElement.GetProperty("result")[0].GetProperty("output").GetProperty("servedBy").GetString()),
            () => Assert.Equal("beta", betaDoc.RootElement.GetProperty("result")[0].GetProperty("output").GetProperty("servedBy").GetString()));
    }

    private static byte[] BuildMarkedResponse(byte[] requestBytes, string servedBy)
    {
        var request = JsonSerializer.Deserialize<TaskRequest>(
            requestBytes,
            WorkerJsonOptions.Instance)!;

        var response = new TaskResponse
        {
            McpTask = request.McpTask,
            Status = TaskResponse.StatusOk,
            Output = JsonSerializer.SerializeToElement(new { servedBy }),
            Error = string.Empty,
        };

        return JsonSerializer.SerializeToUtf8Bytes(response, WorkerJsonOptions.Instance);
    }

    private static ToolInvoker BuildInvoker()
    {
        var workerClient = new WorkerClient(TimeSpan.FromSeconds(5));
        var batcher = new EditorConfigBatcher(workerClient, "autocontext-test-workspace-unused", NullLogger<EditorConfigBatcher>.Instance);
        return new ToolInvoker(workerClient, batcher);
    }

    private static McpWorkersCatalog BuildCatalog(
        params (string Id, string Name, IReadOnlyList<McpToolDefinition> Definitions)[] workers)
    {
        var list = new List<McpWorker>(workers.Length);

        foreach (var (id, name, definitions) in workers)
        {
            list.Add(new McpWorker
            {
                Id = id,
                Name = name,
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
