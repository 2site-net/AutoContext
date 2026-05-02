namespace AutoContext.Mcp.Server.Tests.Tools.Invocation;

using System.Collections.Concurrent;
using System.Text.Json;

using AutoContext.Mcp.Server.Config;
using AutoContext.Mcp.Server.EditorConfig;
using AutoContext.Mcp.Server.Registry;
using AutoContext.Mcp.Server.Tests.Testing.Utils;
using AutoContext.Mcp.Server.Tools.Invocation;
using AutoContext.Mcp.Server.Tools.Results;
using AutoContext.Mcp.Server.Workers;
using AutoContext.Mcp.Server.Workers.Protocol;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class ToolInvokerTests
{
    [Fact]
    public async Task Should_compose_uniform_envelope_with_declared_task_order()
    {
        // Arrange
        var workerId = PipeServerHarness.UniqueWorkerId();
        var pipeName = PipeServerHarness.PipeNameFor(workerId);
        var worker = BuildWorker(workerId);
        var tool = BuildTool("compose_tool", BuildTask("task_a"), BuildTask("task_b"), BuildTask("task_c"));
        var invoker = BuildInvoker();

        var serverTask = PipeServerHarness.RunMultiAsync(
            pipeName,
            connectionCount: 3,
            handler: requestBytes => OkResponse(requestBytes, output: new { ran = true }),
            cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var envelope = await invoker.InvokeAsync(
            worker,
            tool,
            EmptyData(),
            "corr-test",
            TestContext.Current.CancellationToken);
        await serverTask;

        // Assert
        Assert.Multiple(
            () => Assert.Equal("compose_tool", envelope.Tool),
            () => Assert.Equal(ToolResultEnvelope.StatusOk, envelope.Status),
            () => Assert.Equal(3, envelope.Summary.TaskCount),
            () => Assert.Equal(3, envelope.Summary.SuccessCount),
            () => Assert.Equal(0, envelope.Summary.FailureCount),
            () => Assert.Equal("task_a", envelope.Result[0].Task),
            () => Assert.Equal("task_b", envelope.Result[1].Task),
            () => Assert.Equal("task_c", envelope.Result[2].Task),
            () => Assert.Empty(envelope.Errors));
    }

    [Fact]
    public async Task Should_run_multiple_tasks_concurrently()
    {
        // Arrange
        var workerId = PipeServerHarness.UniqueWorkerId();
        var pipeName = PipeServerHarness.PipeNameFor(workerId);
        var worker = BuildWorker(workerId);
        var tool = BuildTool("parallel_tool", BuildTask("task_a"), BuildTask("task_b"));
        var invoker = BuildInvoker();

        var observed = new ConcurrencyObserver();
        var serverTask = PipeServerHarness.RunMultiAsync(
            pipeName,
            connectionCount: 2,
            handler: requestBytes =>
            {
                observed.Enter();
                Thread.Sleep(50);
                observed.Exit();
                return OkResponse(requestBytes, output: new { ran = true });
            },
            cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var envelope = await invoker.InvokeAsync(
            worker,
            tool,
            EmptyData(),
            "corr-test",
            TestContext.Current.CancellationToken);
        await serverTask;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(ToolResultEnvelope.StatusOk, envelope.Status),
            () => Assert.Equal(2, observed.MaxConcurrent));
    }

    [Fact]
    public async Task Should_inject_per_task_editorconfig_slice()
    {
        // Arrange
        var workspaceRole = PipeServerHarness.UniqueRole();
        var workspacePipeName = PipeServerHarness.AddressFor(workspaceRole);
        var toolWorkerId = PipeServerHarness.UniqueWorkerId();
        var toolPipeName = PipeServerHarness.PipeNameFor(toolWorkerId);

        var worker = BuildWorker(toolWorkerId);
        var tool = BuildTool(
            "editorconfig_tool",
            BuildTask("style_task", editorConfig: ["csharp_prefer_braces"]),
            BuildTask("plain_task"));

        var workerClient = new WorkerClient(TimeSpan.FromSeconds(5));
        var batcher = new EditorConfigBatcher(workerClient, workspaceRole, NullLogger<EditorConfigBatcher>.Instance);
        var invoker = new ToolInvoker(workerClient, batcher);

        var workspaceTask = PipeServerHarness.RunOneShotAsync(
            workspacePipeName,
            handler: requestBytes =>
            {
                var request = JsonSerializer.Deserialize<TaskRequest>(
                    requestBytes,
                    WorkerJsonOptions.Instance)!;
                Assert.Equal(EditorConfigBatcher.ResolveTaskName, request.McpTask);

                var response = new TaskResponse
                {
                    McpTask = EditorConfigBatcher.ResolveTaskName,
                    Status = TaskResponse.StatusOk,
                    Output = JsonSerializer.SerializeToElement(
                        new { csharp_prefer_braces = "true" }),
                    Error = string.Empty,
                };
                return JsonSerializer.SerializeToUtf8Bytes(response, WorkerJsonOptions.Instance);
            },
            cancellationToken: TestContext.Current.CancellationToken);

        var observedKeys = new ConcurrentDictionary<string, string[]>();
        var toolServerTask = PipeServerHarness.RunMultiAsync(
            toolPipeName,
            connectionCount: 2,
            handler: requestBytes =>
            {
                var request = JsonSerializer.Deserialize<TaskRequest>(
                    requestBytes,
                    WorkerJsonOptions.Instance)!;
                observedKeys[request.McpTask] = [.. request.EditorConfig.Keys];
                return OkResponse(requestBytes, output: new { ran = true });
            },
            cancellationToken: TestContext.Current.CancellationToken);

        var data = JsonSerializer.SerializeToElement(
            new { originalPath = @"C:\repo\Foo.cs" },
            WorkerJsonOptions.Instance);

        // Act
        var envelope = await invoker.InvokeAsync(
            worker,
            tool,
            data,
            "corr-test",
            TestContext.Current.CancellationToken);
        await Task.WhenAll(workspaceTask, toolServerTask);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(ToolResultEnvelope.StatusOk, envelope.Status),
            () => Assert.Equal(["csharp_prefer_braces"], observedKeys["style_task"]),
            () => Assert.Empty(observedKeys["plain_task"]));
    }

    [Fact]
    public async Task Should_skip_editorconfig_when_path_is_missing()
    {
        // Arrange
        var workerId = PipeServerHarness.UniqueWorkerId();
        var pipeName = PipeServerHarness.PipeNameFor(workerId);
        var worker = BuildWorker(workerId);
        var tool = BuildTool(
            "no_path_tool",
            BuildTask("style_task", editorConfig: ["csharp_prefer_braces"]));
        var invoker = BuildInvoker();

        var observedKeys = new ConcurrentBag<string>();
        var serverTask = PipeServerHarness.RunOneShotAsync(
            pipeName,
            handler: requestBytes =>
            {
                var request = JsonSerializer.Deserialize<TaskRequest>(
                    requestBytes,
                    WorkerJsonOptions.Instance)!;
                foreach (var key in request.EditorConfig.Keys)
                {
                    observedKeys.Add(key);
                }
                return OkResponse(requestBytes, output: new { ran = true });
            },
            cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var envelope = await invoker.InvokeAsync(
            worker,
            tool,
            EmptyData(),
            "corr-test",
            TestContext.Current.CancellationToken);
        await serverTask;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(ToolResultEnvelope.StatusOk, envelope.Status),
            () => Assert.Empty(observedKeys));
    }

    [Fact]
    public async Task Should_isolate_pipe_failures_per_task()
    {
        // Arrange
        var workerId = PipeServerHarness.UniqueWorkerId();
        var pipeName = PipeServerHarness.PipeNameFor(workerId);
        var worker = BuildWorker(workerId);
        var tool = BuildTool("partial_tool", BuildTask("ok_task"), BuildTask("fail_task"));
        var invoker = BuildInvoker();

        var serverTask = PipeServerHarness.RunMultiAsync(
            pipeName,
            connectionCount: 2,
            handler: requestBytes =>
            {
                var request = JsonSerializer.Deserialize<TaskRequest>(
                    requestBytes,
                    WorkerJsonOptions.Instance)!;

                if (string.Equals(request.McpTask, "fail_task", StringComparison.Ordinal))
                {
                    var error = new TaskResponse
                    {
                        McpTask = request.McpTask,
                        Status = TaskResponse.StatusError,
                        Output = null,
                        Error = "boom",
                    };
                    return JsonSerializer.SerializeToUtf8Bytes(error, WorkerJsonOptions.Instance);
                }

                return OkResponse(requestBytes, output: new { ran = true });
            },
            cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var envelope = await invoker.InvokeAsync(
            worker,
            tool,
            EmptyData(),
            "corr-test",
            TestContext.Current.CancellationToken);
        await serverTask;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(ToolResultEnvelope.StatusPartial, envelope.Status),
            () => Assert.Equal(1, envelope.Summary.SuccessCount),
            () => Assert.Equal(1, envelope.Summary.FailureCount),
            () => Assert.Equal(ToolResultEnvelope.StatusOk, envelope.Result[0].Status),
            () => Assert.Equal(ToolResultEnvelope.StatusError, envelope.Result[1].Status),
            () => Assert.Equal("boom", envelope.Result[1].Error));
    }

    [Fact]
    public async Task Should_skip_disabled_tasks_during_dispatch()
    {
        // Arrange
        var workerId = PipeServerHarness.UniqueWorkerId();
        var pipeName = PipeServerHarness.PipeNameFor(workerId);
        var worker = BuildWorker(workerId);
        var tool = BuildTool("filtered_tool", BuildTask("task_a"), BuildTask("task_b"), BuildTask("task_c"));
        var snapshot = new AutoContextConfigSnapshot();
        snapshot.Update(new AutoContextConfigSnapshotDto
        {
            DisabledTasks = new Dictionary<string, List<string>>
            {
                ["filtered_tool"] = ["task_b"],
            },
        });
        var invoker = BuildInvoker(snapshot);

        var observed = new ConcurrentBag<string>();
        var serverTask = PipeServerHarness.RunMultiAsync(
            pipeName,
            connectionCount: 2,
            handler: requestBytes =>
            {
                var request = JsonSerializer.Deserialize<TaskRequest>(
                    requestBytes,
                    WorkerJsonOptions.Instance)!;
                observed.Add(request.McpTask);
                return OkResponse(requestBytes, output: new { ran = true });
            },
            cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var envelope = await invoker.InvokeAsync(
            worker,
            tool,
            EmptyData(),
            "corr-test",
            TestContext.Current.CancellationToken);
        await serverTask;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(ToolResultEnvelope.StatusOk, envelope.Status),
            () => Assert.Equal(2, envelope.Summary.TaskCount),
            () => Assert.Equal("task_a", envelope.Result[0].Task),
            () => Assert.Equal("task_c", envelope.Result[1].Task),
            () => Assert.DoesNotContain(envelope.Result, r => string.Equals(r.Task, "task_b", StringComparison.Ordinal)),
            () => Assert.DoesNotContain("task_b", observed));
    }

    [Fact]
    public async Task Should_return_empty_result_when_all_tasks_are_disabled()
    {
        // Arrange
        var workerId = PipeServerHarness.UniqueWorkerId();
        var worker = BuildWorker(workerId);
        var tool = BuildTool("all_disabled_tool", BuildTask("task_a"), BuildTask("task_b"));
        var snapshot = new AutoContextConfigSnapshot();
        snapshot.Update(new AutoContextConfigSnapshotDto
        {
            DisabledTasks = new Dictionary<string, List<string>>
            {
                ["all_disabled_tool"] = ["task_a", "task_b"],
            },
        });
        var invoker = BuildInvoker(snapshot);

        // Act
        var envelope = await invoker.InvokeAsync(
            worker,
            tool,
            EmptyData(),
            "corr-test",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(ToolResultEnvelope.StatusError, envelope.Status),
            () => Assert.Equal(0, envelope.Summary.TaskCount),
            () => Assert.Equal(0, envelope.Summary.SuccessCount),
            () => Assert.Equal(0, envelope.Summary.FailureCount),
            () => Assert.Empty(envelope.Result),
            () => Assert.Single(envelope.Errors),
            () => Assert.Equal(ToolResultErrorCodes.AllTasksDisabled, envelope.Errors[0].Code));
    }

    private static ToolInvoker BuildInvoker(AutoContextConfigSnapshot? configSnapshot = null)
    {
        var workerClient = new WorkerClient(TimeSpan.FromSeconds(5));
        var batcher = new EditorConfigBatcher(workerClient, "autocontext-test-workspace-unused", NullLogger<EditorConfigBatcher>.Instance);
        return new ToolInvoker(workerClient, batcher, configSnapshot, NullLogger<ToolInvoker>.Instance);
    }

    private static McpWorker BuildWorker(string workerId) => new()
    {
        Id = workerId,
        Name = "AutoContext.Worker.Test",
        Tools = [],
    };

    private static McpTaskDefinition BuildTask(string name, IReadOnlyList<string>? editorConfig = null) => new()
    {
        Name = name,
        EditorConfig = editorConfig ?? [],
    };

    private static McpToolDefinition BuildTool(string name, params McpTaskDefinition[] tasks) => new()
    {
        Name = name,
        Description = "Test tool.",
        Parameters = new Dictionary<string, McpToolParameter>(StringComparer.Ordinal),
        Tasks = tasks,
    };

    private static JsonElement EmptyData() =>
        JsonSerializer.SerializeToElement(new { }, WorkerJsonOptions.Instance);

    private static byte[] OkResponse(byte[] requestBytes, object output)
    {
        var request = JsonSerializer.Deserialize<TaskRequest>(
            requestBytes,
            WorkerJsonOptions.Instance)!;

        var response = new TaskResponse
        {
            McpTask = request.McpTask,
            Status = TaskResponse.StatusOk,
            Output = JsonSerializer.SerializeToElement(output, WorkerJsonOptions.Instance),
            Error = string.Empty,
        };

        return JsonSerializer.SerializeToUtf8Bytes(response, WorkerJsonOptions.Instance);
    }

    private sealed class ConcurrencyObserver
    {
        private readonly Lock _gate = new();
        private int _current;

        public int MaxConcurrent { get; private set; }

        public void Enter()
        {
            lock (_gate)
            {
                _current++;
                if (_current > MaxConcurrent)
                {
                    MaxConcurrent = _current;
                }
            }
        }

        public void Exit()
        {
            lock (_gate)
            {
                _current--;
            }
        }
    }
}
