namespace AutoContext.Worker.Shared.Tests.Hosting;

using System.IO.Pipes;
using System.Text.Json;

using AutoContext.Mcp;
using AutoContext.Worker.Hosting;
using AutoContext.Worker.Shared.Tests.Testing.Fakes;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

public sealed class McpToolServiceTests
{
    private const string TestReadyMarker = "[AutoContext.Worker.Tests] Ready.";

    [Fact]
    public async Task Should_dispatch_request_to_matching_task_and_return_ok_envelope()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var pipeName = $"ac-test-{Guid.NewGuid():N}";
        using var sut = CreateSut(pipeName, [new EchoTaskFake()]);
        await sut.StartAsync(ct);

        try
        {
            // Act
            var response = await SendAsync(pipeName, new
            {
                mcpTask = "echo",
                data = new { value = 42 },
                editorconfig = new { },
            }, ct);

            // Assert
            Assert.Multiple(
                () => Assert.Equal("echo", response.GetProperty("mcpTask").GetString()),
                () => Assert.Equal("ok", response.GetProperty("status").GetString()),
                () => Assert.Equal(string.Empty, response.GetProperty("error").GetString()),
                () => Assert.Equal(42, response.GetProperty("output").GetProperty("value").GetInt32()));
        }
        finally
        {
            await sut.StopAsync(ct);
        }
    }

    [Fact]
    public async Task Should_return_error_envelope_for_unknown_task()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var pipeName = $"ac-test-{Guid.NewGuid():N}";
        using var sut = CreateSut(pipeName, []);
        await sut.StartAsync(ct);

        try
        {
            // Act
            var response = await SendAsync(pipeName, new
            {
                mcpTask = "does_not_exist",
                data = new { },
                editorconfig = new { },
            }, ct);

            // Assert
            Assert.Multiple(
                () => Assert.Equal("does_not_exist", response.GetProperty("mcpTask").GetString()),
                () => Assert.Equal("error", response.GetProperty("status").GetString()),
                () => Assert.Equal(JsonValueKind.Null, response.GetProperty("output").ValueKind),
                () => Assert.Contains("Unknown task", response.GetProperty("error").GetString(), StringComparison.Ordinal));
        }
        finally
        {
            await sut.StopAsync(ct);
        }
    }

    [Fact]
    public async Task Should_return_error_envelope_when_task_throws()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var pipeName = $"ac-test-{Guid.NewGuid():N}";
        using var sut = CreateSut(pipeName, [new ThrowingTaskFake()]);
        await sut.StartAsync(ct);

        try
        {
            // Act
            var response = await SendAsync(pipeName, new
            {
                mcpTask = "boom",
                data = new { },
                editorconfig = new { },
            }, ct);

            // Assert
            Assert.Multiple(
                () => Assert.Equal("error", response.GetProperty("status").GetString()),
                () => Assert.Contains("kaboom", response.GetProperty("error").GetString(), StringComparison.Ordinal));
        }
        finally
        {
            await sut.StopAsync(ct);
        }
    }

    [Fact]
    public async Task Should_let_critical_exceptions_escape_dispatcher()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var pipeName = $"ac-test-{Guid.NewGuid():N}";
        using var sut = CreateSut(pipeName, [new CriticalThrowingTaskFake()]);
        await sut.StartAsync(ct);

        try
        {
            // Act + Assert: a critical exception (e.g. OutOfMemoryException)
            // must NOT be converted into an error envelope. The dispatcher
            // re-throws, the connection drops without writing a response,
            // and the client observes a null read.
            await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(5000, ct);

            var bytes = JsonSerializer.SerializeToUtf8Bytes(
                new
                {
                    mcpTask = "critical_boom",
                    data = new { },
                    editorconfig = new { },
                },
                McpToolService.WorkerJsonOptions);
            var channel = new WorkerProtocolChannel(client);
            await channel.WriteAsync(bytes, ct);

            var responseBytes = await channel.ReadAsync(ct);
            Assert.Null(responseBytes);
        }
        finally
        {
            await sut.StopAsync(ct);
        }
    }

    private static McpToolService CreateSut(string pipeName, IMcpTask[] tasks)
    {
        var options = Options.Create(new WorkerHostOptions
        {
            Pipe = pipeName,
            ReadyMarker = TestReadyMarker,
        });

        return new McpToolService(options, tasks, NullLogger<McpToolService>.Instance);
    }

    private static async Task<JsonElement> SendAsync(string pipeName, object request, CancellationToken ct)
    {
        await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(5000, ct);

        var bytes = JsonSerializer.SerializeToUtf8Bytes(request, McpToolService.WorkerJsonOptions);
        var channel = new WorkerProtocolChannel(client);
        await channel.WriteAsync(bytes, ct);

        var responseBytes = await channel.ReadAsync(ct);
        Assert.NotNull(responseBytes);

        return JsonDocument.Parse(responseBytes!).RootElement.Clone();
    }
}
