namespace AutoContext.Mcp.Tools.Tests.Dispatch;

using System.Text.Json;

using AutoContext.Mcp.Tools.Dispatch;
using AutoContext.Mcp.Tools.EditorConfig;
using AutoContext.Mcp.Tools.Envelope;
using AutoContext.Mcp.Tools.Manifest;
using AutoContext.Mcp.Tools.Pipe;
using AutoContext.Mcp.Tools.Tests.Testing.Utils;
using AutoContext.Mcp.Tools.Wire;

public sealed class ToolDelegateFactoryTests
{
    [Fact]
    public void Should_build_delegate_per_tool_keyed_by_definition_name()
    {
        // Arrange
        var manifest = BuildManifest(
            ("group_a", [BuildTool("tool_one", "task_one")]),
            ("group_b", [BuildTool("tool_two", "task_two")]));
        var invoker = BuildInvoker();

        // Act
        var delegates = ToolDelegateFactory.Build(manifest, invoker);

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
        var manifest = BuildManifest(
            ("group_a", [BuildTool("dup_tool", "task_a")]),
            ("group_b", [BuildTool("dup_tool", "task_b")]));
        var invoker = BuildInvoker();

        // Act + Assert
        Assert.Throws<InvalidOperationException>(() => ToolDelegateFactory.Build(manifest, invoker));
    }

    [Fact]
    public async Task Should_return_serialized_envelope_json_when_delegate_invoked()
    {
        // Arrange
        var endpoint = PipeServerHarness.UniqueEndpoint();
        var manifest = BuildManifest(
            ("group_a", [BuildTool("invoke_tool", "task_x")]),
            endpoint);
        var pipeClient = new WorkerPipeClient(TimeSpan.FromSeconds(5));
        var batcher = new EditorConfigBatcher(pipeClient, "autocontext-test-workspace-unused");
        var invoker = new ToolInvoker(pipeClient, batcher);

        var serverTask = PipeServerHarness.RunOneShotAsync(
            endpoint,
            handler: requestBytes =>
            {
                var request = JsonSerializer.Deserialize<TaskWireRequest>(
                    requestBytes,
                    WireJsonOptions.Instance)!;

                var response = new TaskWireResponse
                {
                    McpTask = request.McpTask,
                    Status = TaskWireResponse.StatusOk,
                    Output = JsonSerializer.SerializeToElement(new { ok = true }),
                    Error = string.Empty,
                };

                return JsonSerializer.SerializeToUtf8Bytes(response, WireJsonOptions.Instance);
            },
            ct: TestContext.Current.CancellationToken);

        var delegates = ToolDelegateFactory.Build(manifest, invoker);
        var data = JsonSerializer.SerializeToElement(new { }, WireJsonOptions.Instance);

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
        var pipeClient = new WorkerPipeClient(TimeSpan.FromSeconds(5));
        var batcher = new EditorConfigBatcher(pipeClient, "autocontext-test-workspace-unused");
        return new ToolInvoker(pipeClient, batcher);
    }

    private static Manifest BuildManifest(
        params (string Name, IReadOnlyList<ManifestTool> Tools)[] groups) =>
        BuildManifest(groups, endpoint: "autocontext-test-unused");

    private static Manifest BuildManifest(
        (string Name, IReadOnlyList<ManifestTool> Tools) singleGroup,
        string endpoint) =>
        BuildManifest([singleGroup], endpoint);

    private static Manifest BuildManifest(
        (string Name, IReadOnlyList<ManifestTool> Tools)[] groups,
        string endpoint)
    {
        var manifestGroups = new List<ManifestGroup>(groups.Length);

        foreach (var (name, tools) in groups)
        {
            var taskNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (var tool in tools)
            {
                foreach (var taskName in tool.Tasks)
                {
                    taskNames.Add(taskName);
                }
            }

            var tasks = new List<ManifestTask>(taskNames.Count);

            foreach (var taskName in taskNames)
            {
                tasks.Add(new ManifestTask
                {
                    Name = taskName,
                    Version = "1.0.0",
                    Priority = 0,
                });
            }

            manifestGroups.Add(new ManifestGroup
            {
                Tag = name,
                Description = "Test group.",
                Endpoint = endpoint,
                Tools = tools,
                Tasks = tasks,
            });
        }

        return new Manifest
        {
            SchemaVersion = "1",
            Workers = new Dictionary<string, IReadOnlyList<ManifestGroup>>(StringComparer.Ordinal)
            {
                ["test"] = manifestGroups,
            },
        };
    }

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
}
