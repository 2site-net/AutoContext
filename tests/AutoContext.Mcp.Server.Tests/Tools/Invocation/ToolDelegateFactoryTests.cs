namespace AutoContext.Mcp.Server.Tests.Tools.Invocation;

using System.Text.Json;

using AutoContext.Mcp.Server.EditorConfig;
using AutoContext.Mcp.Server.Registry;
using AutoContext.Mcp.Server.Tests.Support.Shared;
using AutoContext.Mcp.Server.Tests.Support.Tools;
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
        var registry = ToolTestFactory.BuildCatalog(
            ("alpha", "AutoContext.Worker.Alpha", [ToolTestFactory.BuildToolFromTaskNames("tool_one", "task_one")]),
            ("beta", "AutoContext.Worker.Beta", [ToolTestFactory.BuildToolFromTaskNames("tool_two", "task_two")]));
        var invoker = ToolTestFactory.BuildInvoker();

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
        var registry = ToolTestFactory.BuildCatalog(
            ("alpha", "AutoContext.Worker.Alpha", [ToolTestFactory.BuildToolFromTaskNames("dup_tool", "task_a")]),
            ("beta", "AutoContext.Worker.Beta", [ToolTestFactory.BuildToolFromTaskNames("dup_tool", "task_b")]));
        var invoker = ToolTestFactory.BuildInvoker();

        // Act + Assert
        Assert.Throws<InvalidOperationException>(() => ToolDelegateFactory.Build(registry, invoker));
    }

    [Fact]
    public async Task Should_return_serialized_envelope_json_when_delegate_invoked()
    {
        // Arrange
        var workerId = PipeServerHarness.UniqueWorkerId();
        var pipeName = PipeServerHarness.PipeNameFor(workerId);
        var registry = ToolTestFactory.BuildCatalog(
            (workerId, "AutoContext.Worker.Alpha", [ToolTestFactory.BuildToolFromTaskNames("invoke_tool", "task_x")]));
        var workerClient = new WorkerClient(TimeSpan.FromSeconds(5));
        var batcher = new EditorConfigBatcher(workerClient, "autocontext-test-workspace-unused", NullLogger<EditorConfigBatcher>.Instance);
        var invoker = new ToolInvoker(workerClient, batcher);

        var serverTask = PipeServerHarness.RunOneShotAsync(
            pipeName,
            handler: requestBytes =>
            {
                var request = JsonSerializer.Deserialize<JsonTaskRequest>(
                    requestBytes,
                    WorkerJsonOptions.Instance)!;

                var response = new JsonTaskResponse
                {
                    McpTask = request.McpTask,
                    Status = JsonTaskResponse.StatusOk,
                    Output = JsonSerializer.SerializeToElement(new { ok = true }),
                    Error = string.Empty,
                };

                return JsonSerializer.SerializeToUtf8Bytes(response, WorkerJsonOptions.Instance);
            },
            cancellationToken: TestContext.Current.CancellationToken);

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
            () => Assert.Equal(JsonToolResultEnvelope.StatusOk, root.GetProperty("status").GetString()),
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

        var registry = ToolTestFactory.BuildCatalog(
            (alphaWorkerId, "AutoContext.Worker.Alpha", [ToolTestFactory.BuildToolFromTaskNames("alpha_tool", "task_alpha")]),
            (betaWorkerId, "AutoContext.Worker.Beta", [ToolTestFactory.BuildToolFromTaskNames("beta_tool", "task_beta")]));

        var workerClient = new WorkerClient(TimeSpan.FromSeconds(5));
        var batcher = new EditorConfigBatcher(workerClient, "autocontext-test-workspace-unused", NullLogger<EditorConfigBatcher>.Instance);
        var invoker = new ToolInvoker(workerClient, batcher);

        var alphaServerTask = PipeServerHarness.RunOneShotAsync(
            alphaPipeName,
            handler: requestBytes => ToolTestFactory.BuildMarkedResponse(requestBytes, servedBy: "alpha"),
            cancellationToken: TestContext.Current.CancellationToken);
        var betaServerTask = PipeServerHarness.RunOneShotAsync(
            betaPipeName,
            handler: requestBytes => ToolTestFactory.BuildMarkedResponse(requestBytes, servedBy: "beta"),
            cancellationToken: TestContext.Current.CancellationToken);

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
}
