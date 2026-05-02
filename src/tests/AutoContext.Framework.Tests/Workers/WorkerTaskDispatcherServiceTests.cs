namespace AutoContext.Framework.Tests.Workers;

using System.IO.Pipes;
using System.Text.Json;

using AutoContext.Mcp;
using AutoContext.Framework.Workers;
using AutoContext.Framework.Transport;
using AutoContext.Framework.Tests.Testing.Fakes;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

public sealed class WorkerTaskDispatcherServiceTests
{
    private const string TestReadyMarker = "[AutoContext.Worker.Tests] Ready.";

    [Fact]
    public async Task Should_dispatch_request_to_matching_task_and_return_ok_envelope()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = $"ac-test-{Guid.NewGuid():N}";
        using var sut = CreateSut(pipeName, [new EchoTaskFake()]);
        await sut.StartAsync(cancellationToken);

        try
        {
            // Act
            var response = await SendAsync(pipeName, new
            {
                mcpTask = "echo",
                data = new { value = 42 },
                editorconfig = new { },
            }, cancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.Equal("echo", response.GetProperty("mcpTask").GetString()),
                () => Assert.Equal("ok", response.GetProperty("status").GetString()),
                () => Assert.Equal(string.Empty, response.GetProperty("error").GetString()),
                () => Assert.Equal(42, response.GetProperty("output").GetProperty("value").GetInt32()));
        }
        finally
        {
            await sut.StopAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task Should_return_error_envelope_for_unknown_task()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = $"ac-test-{Guid.NewGuid():N}";
        using var sut = CreateSut(pipeName, []);
        await sut.StartAsync(cancellationToken);

        try
        {
            // Act
            var response = await SendAsync(pipeName, new
            {
                mcpTask = "does_not_exist",
                data = new { },
                editorconfig = new { },
            }, cancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.Equal("does_not_exist", response.GetProperty("mcpTask").GetString()),
                () => Assert.Equal("error", response.GetProperty("status").GetString()),
                () => Assert.Equal(JsonValueKind.Null, response.GetProperty("output").ValueKind),
                () => Assert.Contains("Unknown task", response.GetProperty("error").GetString(), StringComparison.Ordinal));
        }
        finally
        {
            await sut.StopAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task Should_return_error_envelope_when_task_throws()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = $"ac-test-{Guid.NewGuid():N}";
        using var sut = CreateSut(pipeName, [new ThrowingTaskFake()]);
        await sut.StartAsync(cancellationToken);

        try
        {
            // Act
            var response = await SendAsync(pipeName, new
            {
                mcpTask = "boom",
                data = new { },
                editorconfig = new { },
            }, cancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.Equal("error", response.GetProperty("status").GetString()),
                () => Assert.Contains("kaboom", response.GetProperty("error").GetString(), StringComparison.Ordinal));
        }
        finally
        {
            await sut.StopAsync(cancellationToken);
        }
    }

    [Fact]
    public async Task Should_let_critical_exceptions_escape_dispatcher()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var pipeName = $"ac-test-{Guid.NewGuid():N}";
        using var sut = CreateSut(pipeName, [new CriticalThrowingTaskFake()]);
        await sut.StartAsync(cancellationToken);

        try
        {
            // Act + Assert: a critical exception (e.g. OutOfMemoryException)
            // must NOT be converted into an error envelope. The dispatcher
            // re-throws, the connection drops without writing a response,
            // and the client observes a null read.
            await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(5000, cancellationToken);

            var bytes = JsonSerializer.SerializeToUtf8Bytes(
                new
                {
                    mcpTask = "critical_boom",
                    data = new { },
                    editorconfig = new { },
                },
                WorkerTaskDispatcherService.WorkerJsonOptions);
            var channel = new LengthPrefixedFrameCodec(client);
            await channel.WriteAsync(bytes, cancellationToken);

            var responseBytes = await channel.ReadAsync(cancellationToken);
            Assert.Null(responseBytes);
        }
        finally
        {
            await sut.StopAsync(cancellationToken);
        }
    }

    private static WorkerTaskDispatcherService CreateSut(string pipeName, IMcpTask[] tasks)
    {
        var options = Options.Create(new WorkerHostOptions
        {
            Pipe = pipeName,
            ReadyMarker = TestReadyMarker,
        });

        return new WorkerTaskDispatcherService(options, tasks, NullLogger<WorkerTaskDispatcherService>.Instance);
    }

    private static async Task<JsonElement> SendAsync(string pipeName, object request, CancellationToken cancellationToken)
    {
        await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(5000, cancellationToken);

        var bytes = JsonSerializer.SerializeToUtf8Bytes(request, WorkerTaskDispatcherService.WorkerJsonOptions);
        var channel = new LengthPrefixedFrameCodec(client);
        await channel.WriteAsync(bytes, cancellationToken);

        var responseBytes = await channel.ReadAsync(cancellationToken);
        Assert.NotNull(responseBytes);

        return JsonDocument.Parse(responseBytes!).RootElement.Clone();
    }
}
