namespace AutoContext.Mcp.Server.Tests.EditorConfig;

using System.Text.Json;

using AutoContext.Mcp.Server.EditorConfig;
using AutoContext.Mcp.Server.Registry;
using AutoContext.Mcp.Server.Tests.Testing.Utils;
using AutoContext.Mcp.Server.Workers;
using AutoContext.Mcp.Server.Workers.Protocol;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class EditorConfigBatcherTests
{
    private static readonly NullLogger<EditorConfigBatcher> Logger = NullLogger<EditorConfigBatcher>.Instance;
    private static readonly string[] ExpectedUnion =
    [
        "csharp_prefer_braces",
        "csharp_style_namespace_declarations",
        "dotnet_sort_system_directives_first",
    ];

    [Fact]
    public async Task Should_skip_pipe_call_when_no_task_declares_keys()
    {
        // Arrange
        var client = new WorkerClient(TimeSpan.FromSeconds(1));
        var batcher = new EditorConfigBatcher(client, "autocontext-test-unbound", Logger);
        var tasks = new McpTaskDefinition[]
        {
            BuildTask("task_a"),
            BuildTask("task_b"),
        };

        // Act
        var result = await batcher.ResolveAsync(
            "/abs/file.cs",
            tasks,
            "corr-test",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.False(result.ResolutionFailed),
            () => Assert.Null(result.FailureMessage),
            () => Assert.Equal(2, result.Slices.Count),
            () => Assert.Empty(result.Slices["task_a"]),
            () => Assert.Empty(result.Slices["task_b"]));
    }

    [Fact]
    public async Task Should_send_union_of_keys_and_slice_per_task()
    {
        // Arrange
        var role = PipeServerHarness.UniqueRole();
        var pipeName = PipeServerHarness.AddressFor(role);
        var client = new WorkerClient(TimeSpan.FromSeconds(5));
        var batcher = new EditorConfigBatcher(client, role, Logger);
        var tasks = new[]
        {
            BuildTask("task_a", "csharp_prefer_braces", "dotnet_sort_system_directives_first"),
            BuildTask("task_b", "csharp_prefer_braces", "csharp_style_namespace_declarations"),
        };
        string[]? observedKeys = null;
        string? observedPath = null;
        var serverTask = PipeServerHarness.RunOneShotAsync(
            pipeName,
            handler: requestBytes =>
            {
                var request = JsonSerializer.Deserialize<JsonElement>(requestBytes);
                Assert.Equal(EditorConfigBatcher.ResolveTaskName, request.GetProperty("mcpTask").GetString());

                var data = request.GetProperty("data");
                observedPath = data.GetProperty("path").GetString();
                observedKeys = [.. data.GetProperty("keys").EnumerateArray().Select(k => k.GetString() ?? string.Empty)];

                var serverResponse = new TaskResponse
                {
                    McpTask = EditorConfigBatcher.ResolveTaskName,
                    Status = TaskResponse.StatusOk,
                    Output = JsonSerializer.SerializeToElement(new Dictionary<string, string>
                    {
                        ["csharp_prefer_braces"] = "true",
                        ["dotnet_sort_system_directives_first"] = "true",
                    }),
                    Error = string.Empty,
                };

                return JsonSerializer.SerializeToUtf8Bytes(serverResponse);
            },
            cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var result = await batcher.ResolveAsync(
            "/abs/Foo.cs",
            tasks,
            "corr-test",
            TestContext.Current.CancellationToken);
        await serverTask;

        // Assert
        Assert.Multiple(
            () => Assert.False(result.ResolutionFailed),
            () => Assert.Equal("/abs/Foo.cs", observedPath),
            () => Assert.Equal(ExpectedUnion, observedKeys),
            () => Assert.Equal(2, result.Slices["task_a"].Count),
            () => Assert.Equal("true", result.Slices["task_a"]["csharp_prefer_braces"]),
            () => Assert.Equal("true", result.Slices["task_a"]["dotnet_sort_system_directives_first"]),
            () => Assert.Single(result.Slices["task_b"]),
            () => Assert.Equal("true", result.Slices["task_b"]["csharp_prefer_braces"]));
    }

    [Fact]
    public async Task Should_degrade_to_empty_slices_on_worker_error()
    {
        // Arrange
        var role = PipeServerHarness.UniqueRole();
        var pipeName = PipeServerHarness.AddressFor(role);
        var client = new WorkerClient(TimeSpan.FromSeconds(5));
        var batcher = new EditorConfigBatcher(client, role, Logger);
        var tasks = new[] { BuildTask("task_a", "csharp_prefer_braces") };
        var serverTask = PipeServerHarness.RunOneShotAsync(
            pipeName,
            handler: _ =>
            {
                var serverResponse = new TaskResponse
                {
                    McpTask = EditorConfigBatcher.ResolveTaskName,
                    Status = TaskResponse.StatusError,
                    Output = null,
                    Error = "parse failure",
                };

                return JsonSerializer.SerializeToUtf8Bytes(serverResponse);
            },
            cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var result = await batcher.ResolveAsync(
            "/abs/Foo.cs",
            tasks,
            "corr-test",
            TestContext.Current.CancellationToken);
        await serverTask;

        // Assert
        Assert.Multiple(
            () => Assert.True(result.ResolutionFailed),
            () => Assert.Equal("parse failure", result.FailureMessage),
            () => Assert.Single(result.Slices),
            () => Assert.Empty(result.Slices["task_a"]));
    }

    [Fact]
    public async Task Should_degrade_when_worker_returns_response_for_unexpected_task()
    {
        // Arrange
        var role = PipeServerHarness.UniqueRole();
        var pipeName = PipeServerHarness.AddressFor(role);
        var client = new WorkerClient(TimeSpan.FromSeconds(5));
        var batcher = new EditorConfigBatcher(client, role, Logger);
        var tasks = new[] { BuildTask("task_a", "csharp_prefer_braces") };
        var serverTask = PipeServerHarness.RunOneShotAsync(
            pipeName,
            handler: _ =>
            {
                var serverResponse = new TaskResponse
                {
                    McpTask = "wrong_task",
                    Status = TaskResponse.StatusOk,
                    Output = JsonSerializer.SerializeToElement(new { }),
                    Error = string.Empty,
                };

                return JsonSerializer.SerializeToUtf8Bytes(serverResponse);
            },
            cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var result = await batcher.ResolveAsync(
            "/abs/Foo.cs",
            tasks,
            "corr-test",
            TestContext.Current.CancellationToken);
        await serverTask;

        // Assert
        Assert.Multiple(
            () => Assert.True(result.ResolutionFailed),
            () => Assert.NotNull(result.FailureMessage),
            () => Assert.Contains("wrong_task", result.FailureMessage!, StringComparison.Ordinal),
            () => Assert.Empty(result.Slices["task_a"]));
    }

    [Fact]
    public async Task Should_degrade_to_empty_slices_on_pipe_failure()
    {
        // Arrange
        var client = new WorkerClient(TimeSpan.FromMilliseconds(150));
        var batcher = new EditorConfigBatcher(client, "unbound-" + Guid.NewGuid().ToString("N"), Logger);
        var tasks = new[] { BuildTask("task_a", "csharp_prefer_braces") };

        // Act
        var result = await batcher.ResolveAsync(
            "/abs/Foo.cs",
            tasks,
            "corr-test",
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.True(result.ResolutionFailed),
            () => Assert.NotNull(result.FailureMessage),
            () => Assert.Empty(result.Slices["task_a"]));
    }

    [Fact]
    public async Task Should_surface_failure_when_worker_output_is_not_object()
    {
        // Arrange
        var role = PipeServerHarness.UniqueRole();
        var pipeName = PipeServerHarness.AddressFor(role);
        var client = new WorkerClient(TimeSpan.FromSeconds(5));
        var batcher = new EditorConfigBatcher(client, role, Logger);
        var tasks = new[] { BuildTask("task_a", "csharp_prefer_braces") };
        var serverTask = PipeServerHarness.RunOneShotAsync(
            pipeName,
            handler: _ =>
            {
                var serverResponse = new TaskResponse
                {
                    McpTask = EditorConfigBatcher.ResolveTaskName,
                    Status = TaskResponse.StatusOk,
                    Output = JsonSerializer.SerializeToElement("not-an-object"),
                    Error = string.Empty,
                };

                return JsonSerializer.SerializeToUtf8Bytes(serverResponse);
            },
            cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var result = await batcher.ResolveAsync(
            "/abs/Foo.cs",
            tasks,
            "corr-test",
            TestContext.Current.CancellationToken);
        await serverTask;

        // Assert
        Assert.Multiple(
            () => Assert.True(result.ResolutionFailed),
            () => Assert.NotNull(result.FailureMessage),
            () => Assert.Contains("non-object", result.FailureMessage!, StringComparison.Ordinal),
            () => Assert.Single(result.Slices),
            () => Assert.Empty(result.Slices["task_a"]));
    }

    [Fact]
    public async Task Should_throw_for_invalid_arguments()
    {
        // Arrange
        var batcher = new EditorConfigBatcher(new WorkerClient(), "autocontext-test", Logger);

        // Act + Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => batcher.ResolveAsync(null!, [], "corr-test", TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(
            () => batcher.ResolveAsync(string.Empty, [], "corr-test", TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => batcher.ResolveAsync("/abs/Foo.cs", null!, "corr-test", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Should_validate_constructor_arguments()
    {
        Assert.Multiple(
            () => Assert.Throws<ArgumentNullException>(() => new EditorConfigBatcher(null!, Logger)),
            () => Assert.Throws<ArgumentNullException>(() => new EditorConfigBatcher(new WorkerClient(), null!, Logger)),
            () => Assert.Throws<ArgumentException>(() => new EditorConfigBatcher(new WorkerClient(), string.Empty, Logger)),
            () => Assert.Throws<ArgumentNullException>(() => new EditorConfigBatcher(new WorkerClient(), null!)));
    }

    private static McpTaskDefinition BuildTask(string name, params string[] keys) => new()
    {
        Name = name,
        EditorConfig = keys,
    };
}
