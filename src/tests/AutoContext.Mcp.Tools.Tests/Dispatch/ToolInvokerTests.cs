namespace AutoContext.Mcp.Tools.Tests.Dispatch;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

using AutoContext.Mcp.Tools.Dispatch;
using AutoContext.Mcp.Tools.EditorConfig;
using AutoContext.Mcp.Tools.Envelope;
using AutoContext.Mcp.Tools.Manifest;
using AutoContext.Mcp.Tools.Pipe;
using AutoContext.Mcp.Tools.Tests.Testing.Utils;
using AutoContext.Mcp.Tools.Wire;

public sealed class ToolInvokerTests
{
    [Fact]
    public async Task Should_compose_uniform_envelope_with_declared_task_order()
    {
        // Arrange
        var endpoint = PipeServerHarness.UniqueEndpoint();
        var group = BuildGroup(
            endpoint,
            BuildTask("task_a", priority: 0),
            BuildTask("task_b", priority: 0),
            BuildTask("task_c", priority: 0));
        var tool = BuildTool("compose_tool", "task_a", "task_b", "task_c");
        var invoker = BuildInvoker();

        var serverTask = PipeServerHarness.RunMultiAsync(
            endpoint,
            connectionCount: 3,
            handler: requestBytes => OkResponse(requestBytes, output: new { ran = true }),
            ct: TestContext.Current.CancellationToken);

        // Act
        var envelope = await invoker.InvokeAsync(
            group,
            tool,
            EmptyData(),
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
    public async Task Should_run_same_priority_tasks_in_parallel()
    {
        // Arrange
        var endpoint = PipeServerHarness.UniqueEndpoint();
        var group = BuildGroup(
            endpoint,
            BuildTask("p1_a", priority: 1),
            BuildTask("p1_b", priority: 1));
        var tool = BuildTool("parallel_tool", "p1_a", "p1_b");
        var invoker = BuildInvoker();

        var observed = new ConcurrencyObserver();
        var serverTask = PipeServerHarness.RunMultiAsync(
            endpoint,
            connectionCount: 2,
            handler: requestBytes =>
            {
                observed.Enter();
                Thread.Sleep(50);
                observed.Exit();
                return OkResponse(requestBytes, output: new { ran = true });
            },
            ct: TestContext.Current.CancellationToken);

        // Act
        var envelope = await invoker.InvokeAsync(
            group,
            tool,
            EmptyData(),
            TestContext.Current.CancellationToken);
        await serverTask;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(ToolResultEnvelope.StatusOk, envelope.Status),
            () => Assert.Equal(2, observed.MaxConcurrent));
    }

    [Fact]
    public async Task Should_run_priority_groups_in_ascending_order()
    {
        // Arrange
        var endpoint = PipeServerHarness.UniqueEndpoint();
        var group = BuildGroup(
            endpoint,
            BuildTask("first", priority: 1),
            BuildTask("second", priority: 2));
        var tool = BuildTool("ordered_tool", "first", "second");
        var invoker = BuildInvoker();

        var startOrder = new ConcurrentQueue<string>();
        var serverTask = PipeServerHarness.RunMultiAsync(
            endpoint,
            connectionCount: 2,
            handler: requestBytes =>
            {
                var request = JsonSerializer.Deserialize<TaskWireRequest>(
                    requestBytes,
                    WireJsonOptions.Instance)!;
                startOrder.Enqueue(request.McpTask);
                Thread.Sleep(20);
                return OkResponse(requestBytes, output: new { ran = true });
            },
            ct: TestContext.Current.CancellationToken);

        // Act
        var envelope = await invoker.InvokeAsync(
            group,
            tool,
            EmptyData(),
            TestContext.Current.CancellationToken);
        await serverTask;

        var observed = startOrder.ToArray();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(ToolResultEnvelope.StatusOk, envelope.Status),
            () => Assert.Equal(["first", "second"], observed));
    }

    [Fact]
    public async Task Should_run_zero_priority_tasks_after_prioritized_groups()
    {
        // Arrange
        var endpoint = PipeServerHarness.UniqueEndpoint();
        var group = BuildGroup(
            endpoint,
            BuildTask("trailing", priority: 0),
            BuildTask("urgent", priority: 1));
        var tool = BuildTool("trailing_tool", "trailing", "urgent");
        var invoker = BuildInvoker();

        var startOrder = new ConcurrentQueue<string>();
        var serverTask = PipeServerHarness.RunMultiAsync(
            endpoint,
            connectionCount: 2,
            handler: requestBytes =>
            {
                var request = JsonSerializer.Deserialize<TaskWireRequest>(
                    requestBytes,
                    WireJsonOptions.Instance)!;
                startOrder.Enqueue(request.McpTask);
                Thread.Sleep(20);
                return OkResponse(requestBytes, output: new { ran = true });
            },
            ct: TestContext.Current.CancellationToken);

        // Act
        var envelope = await invoker.InvokeAsync(
            group,
            tool,
            EmptyData(),
            TestContext.Current.CancellationToken);
        await serverTask;

        var observed = startOrder.ToArray();

        // Assert
        Assert.Multiple(
            () => Assert.Equal(ToolResultEnvelope.StatusOk, envelope.Status),
            () => Assert.Equal(["urgent", "trailing"], observed),
            () => Assert.Equal("trailing", envelope.Result[0].Task),
            () => Assert.Equal("urgent", envelope.Result[1].Task));
    }

    [Fact]
    public async Task Should_inject_per_task_editorconfig_slice()
    {
        // Arrange
        var workspaceEndpoint = PipeServerHarness.UniqueEndpoint();
        var toolEndpoint = PipeServerHarness.UniqueEndpoint();

        var group = BuildGroup(
            toolEndpoint,
            BuildTask("style_task", priority: 1, editorConfig: ["csharp_prefer_braces"]),
            BuildTask("plain_task", priority: 1));
        var tool = BuildTool("editorconfig_tool", "style_task", "plain_task");

        var pipeClient = new WorkerPipeClient(TimeSpan.FromSeconds(5));
        var batcher = new EditorConfigBatcher(pipeClient, workspaceEndpoint);
        var invoker = new ToolInvoker(pipeClient, batcher);

        var workspaceTask = PipeServerHarness.RunOneShotAsync(
            workspaceEndpoint,
            handler: requestBytes =>
            {
                var request = JsonSerializer.Deserialize<TaskWireRequest>(
                    requestBytes,
                    WireJsonOptions.Instance)!;
                Assert.Equal(EditorConfigBatcher.ResolveTaskName, request.McpTask);

                var response = new TaskWireResponse
                {
                    McpTask = EditorConfigBatcher.ResolveTaskName,
                    Status = TaskWireResponse.StatusOk,
                    Output = JsonSerializer.SerializeToElement(
                        new { csharp_prefer_braces = "true" }),
                    Error = string.Empty,
                };
                return JsonSerializer.SerializeToUtf8Bytes(response, WireJsonOptions.Instance);
            },
            ct: TestContext.Current.CancellationToken);

        var observedKeys = new ConcurrentDictionary<string, string[]>();
        var toolServerTask = PipeServerHarness.RunMultiAsync(
            toolEndpoint,
            connectionCount: 2,
            handler: requestBytes =>
            {
                var request = JsonSerializer.Deserialize<TaskWireRequest>(
                    requestBytes,
                    WireJsonOptions.Instance)!;
                observedKeys[request.McpTask] = [.. request.EditorConfig.Keys];
                return OkResponse(requestBytes, output: new { ran = true });
            },
            ct: TestContext.Current.CancellationToken);

        var data = JsonSerializer.SerializeToElement(
            new { originalPath = @"C:\repo\Foo.cs" },
            WireJsonOptions.Instance);

        // Act
        var envelope = await invoker.InvokeAsync(
            group,
            tool,
            data,
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
        var endpoint = PipeServerHarness.UniqueEndpoint();
        var group = BuildGroup(
            endpoint,
            BuildTask("style_task", priority: 1, editorConfig: ["csharp_prefer_braces"]));
        var tool = BuildTool("no_path_tool", "style_task");
        var invoker = BuildInvoker();

        var observedKeys = new ConcurrentBag<string>();
        var serverTask = PipeServerHarness.RunOneShotAsync(
            endpoint,
            handler: requestBytes =>
            {
                var request = JsonSerializer.Deserialize<TaskWireRequest>(
                    requestBytes,
                    WireJsonOptions.Instance)!;
                foreach (var key in request.EditorConfig.Keys)
                {
                    observedKeys.Add(key);
                }
                return OkResponse(requestBytes, output: new { ran = true });
            },
            ct: TestContext.Current.CancellationToken);

        // Act
        var envelope = await invoker.InvokeAsync(
            group,
            tool,
            EmptyData(),
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
        var endpoint = PipeServerHarness.UniqueEndpoint();
        var group = BuildGroup(
            endpoint,
            BuildTask("ok_task", priority: 1),
            BuildTask("fail_task", priority: 1));
        var tool = BuildTool("partial_tool", "ok_task", "fail_task");
        var invoker = BuildInvoker();

        var serverTask = PipeServerHarness.RunMultiAsync(
            endpoint,
            connectionCount: 2,
            handler: requestBytes =>
            {
                var request = JsonSerializer.Deserialize<TaskWireRequest>(
                    requestBytes,
                    WireJsonOptions.Instance)!;

                if (string.Equals(request.McpTask, "fail_task", StringComparison.Ordinal))
                {
                    var error = new TaskWireResponse
                    {
                        McpTask = request.McpTask,
                        Status = TaskWireResponse.StatusError,
                        Output = null,
                        Error = "boom",
                    };
                    return JsonSerializer.SerializeToUtf8Bytes(error, WireJsonOptions.Instance);
                }

                return OkResponse(requestBytes, output: new { ran = true });
            },
            ct: TestContext.Current.CancellationToken);

        // Act
        var envelope = await invoker.InvokeAsync(
            group,
            tool,
            EmptyData(),
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
    public async Task Should_throw_when_tool_references_unknown_task()
    {
        // Arrange
        var group = BuildGroup(PipeServerHarness.UniqueEndpoint());
        var tool = BuildTool("ghost_tool", "missing_task");
        var invoker = BuildInvoker();

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => invoker.InvokeAsync(
                group,
                tool,
                EmptyData(),
                TestContext.Current.CancellationToken));
    }

    private static ToolInvoker BuildInvoker()
    {
        var pipeClient = new WorkerPipeClient(TimeSpan.FromSeconds(5));
        var batcher = new EditorConfigBatcher(pipeClient, "autocontext-test-workspace-unused");
        return new ToolInvoker(pipeClient, batcher);
    }

    private static ManifestGroup BuildGroup(string endpoint, params ManifestTask[] tasks) => new()
    {
        Tag = "test-group",
        Description = "Test group.",
        Endpoint = endpoint,
        Tools = [],
        Tasks = tasks,
    };

    private static ManifestTask BuildTask(string name, int priority, IReadOnlyList<string>? editorConfig = null) => new()
    {
        Name = name,
        Version = "1.0.0",
        Priority = priority,
        EditorConfig = editorConfig ?? [],
    };

    private static ManifestTool BuildTool(string name, params string[] tasks) => new()
    {
        Tag = "test-tool",
        Description = "Test tool.",
        Definition = new ManifestToolDefinition
        {
            Name = name,
            Description = "Test definition.",
            Parameters = new Dictionary<string, ManifestParameter>(StringComparer.Ordinal),
        },
        Tasks = tasks,
    };

    private static JsonElement EmptyData() =>
        JsonSerializer.SerializeToElement(new { }, WireJsonOptions.Instance);

    private static byte[] OkResponse(byte[] requestBytes, object output)
    {
        var request = JsonSerializer.Deserialize<TaskWireRequest>(
            requestBytes,
            WireJsonOptions.Instance)!;

        var response = new TaskWireResponse
        {
            McpTask = request.McpTask,
            Status = TaskWireResponse.StatusOk,
            Output = JsonSerializer.SerializeToElement(output, WireJsonOptions.Instance),
            Error = string.Empty,
        };

        return JsonSerializer.SerializeToUtf8Bytes(response, WireJsonOptions.Instance);
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
