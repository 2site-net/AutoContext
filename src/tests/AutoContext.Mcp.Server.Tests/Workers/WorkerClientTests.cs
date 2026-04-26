namespace AutoContext.Mcp.Server.Tests.Workers;

using System.Collections.Frozen;
using System.IO.Pipes;
using System.Text.Json;

using AutoContext.Mcp.Server.Tests.Testing.Utils;
using AutoContext.Mcp.Server.Workers;
using AutoContext.Mcp.Server.Workers.Protocol;
using AutoContext.Worker.Hosting;

public sealed class WorkerClientTests
{
    [Fact]
    public async Task Should_round_trip_request_and_response()
    {
        // Arrange
        var endpoint = PipeServerHarness.UniqueEndpoint();
        var client = new WorkerClient(TimeSpan.FromSeconds(5));
        var serverTask = PipeServerHarness.RunOneShotAsync(
            endpoint,
            handler: requestBytes =>
            {
                var request = JsonSerializer.Deserialize<TaskRequest>(
                    requestBytes,
                    WorkerJsonOptions.Instance);
                Assert.NotNull(request);
                Assert.Equal("analyze_csharp_coding_style", request.McpTask);

                var serverResponse = new TaskResponse
                {
                    McpTask = request.McpTask,
                    Status = TaskResponse.StatusOk,
                    Output = JsonSerializer.SerializeToElement(new { passed = true }),
                    Error = string.Empty,
                };

                return JsonSerializer.SerializeToUtf8Bytes(
                    serverResponse,
                    WorkerJsonOptions.Instance);
            },
            ct: TestContext.Current.CancellationToken);

        // Act
        var response = await client.InvokeAsync(
            endpoint,
            BuildRequest("analyze_csharp_coding_style"),
            TestContext.Current.CancellationToken);
        await serverTask;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(TaskResponse.StatusOk, response.Status),
            () => Assert.Equal(string.Empty, response.Error),
            () => Assert.NotNull(response.Output),
            () => Assert.True(response.Output!.Value.GetProperty("passed").GetBoolean()));
    }

    [Fact]
    public async Task Should_return_error_when_worker_closes_pipe_without_response()
    {
        // Arrange
        var endpoint = PipeServerHarness.UniqueEndpoint();
        var client = new WorkerClient(TimeSpan.FromSeconds(5));
        var serverTask = PipeServerHarness.RunOneShotAsync(
            endpoint,
            handler: _ => null,
            ct: TestContext.Current.CancellationToken);

        // Act
        var response = await client.InvokeAsync(
            endpoint,
            BuildRequest("task_x"),
            TestContext.Current.CancellationToken);
        await serverTask;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(TaskResponse.StatusError, response.Status),
            () => Assert.Equal("task_x", response.McpTask),
            () => Assert.Contains("closed the pipe", response.Error, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_return_error_when_wait_deadline_elapses()
    {
        // Arrange
        var endpoint = PipeServerHarness.UniqueEndpoint();
        var client = new WorkerClient(TimeSpan.FromMilliseconds(150));
        using var serverGate = new CancellationTokenSource();
        var serverTask = RunHangingServerAsync(endpoint, serverGate.Token);

        // Act
        var response = await client.InvokeAsync(
            endpoint,
            BuildRequest("task_hung"),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(TaskResponse.StatusError, response.Status),
            () => Assert.Contains("wait deadline", response.Error, StringComparison.Ordinal));

        await serverGate.CancelAsync();
        await serverTask;
    }

    [Fact]
    public async Task Should_return_error_when_response_is_invalid_json()
    {
        // Arrange
        var endpoint = PipeServerHarness.UniqueEndpoint();
        var client = new WorkerClient(TimeSpan.FromSeconds(5));
        var serverTask = PipeServerHarness.RunOneShotAsync(
            endpoint,
            handler: _ => "not json"u8.ToArray(),
            ct: TestContext.Current.CancellationToken);

        // Act
        var response = await client.InvokeAsync(
            endpoint,
            BuildRequest("task_bad"),
            TestContext.Current.CancellationToken);
        await serverTask;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(TaskResponse.StatusError, response.Status),
            () => Assert.Contains("not valid JSON", response.Error, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_propagate_caller_cancellation()
    {
        // Arrange
        var endpoint = PipeServerHarness.UniqueEndpoint();
        var client = new WorkerClient(TimeSpan.FromSeconds(30));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act + Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.InvokeAsync(endpoint, BuildRequest("task_x"), cts.Token));
    }

    [Fact]
    public void Should_reject_non_positive_wait_deadline()
    {
        Assert.Multiple(
            () => Assert.Throws<ArgumentOutOfRangeException>(() => new WorkerClient(TimeSpan.Zero)),
            () => Assert.Throws<ArgumentOutOfRangeException>(() => new WorkerClient(TimeSpan.FromMilliseconds(-1))));
    }

    [Fact]
    public async Task Should_reject_null_or_empty_endpoint()
    {
        // Arrange
        var client = new WorkerClient();

        // Act + Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => client.InvokeAsync(null!, BuildRequest("t"), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(
            () => client.InvokeAsync(string.Empty, BuildRequest("t"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_reject_request_with_empty_mcp_task()
    {
        // Arrange
        var client = new WorkerClient();
        var badRequest = new TaskRequest
        {
            McpTask = string.Empty,
            Data = JsonSerializer.SerializeToElement(new { }),
            EditorConfig = FrozenDictionary<string, string>.Empty,
        };

        // Act + Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => client.InvokeAsync("autocontext-test", badRequest, TestContext.Current.CancellationToken));
    }

    private static TaskRequest BuildRequest(string mcpTask) => new()
    {
        McpTask = mcpTask,
        Data = JsonSerializer.SerializeToElement(new { content = "hello" }),
        EditorConfig = FrozenDictionary<string, string>.Empty,
    };

    private static Task RunHangingServerAsync(string endpoint, CancellationToken gate) =>
        Task.Run(
            async () =>
            {
                var server = new NamedPipeServerStream(
                    endpoint,
                    PipeDirection.InOut,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await using (server.ConfigureAwait(false))
                {
                    await server.WaitForConnectionAsync(gate).ConfigureAwait(false);
                    var channel = new WorkerProtocolChannel(server);
                    _ = await channel.ReadAsync(gate).ConfigureAwait(false);

                    try
                    {
                        await Task.Delay(Timeout.Infinite, gate).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }
            },
            TestContext.Current.CancellationToken);
}
