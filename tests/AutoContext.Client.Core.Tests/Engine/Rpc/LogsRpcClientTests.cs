namespace AutoContext.Client.Core.Tests.Engine.Rpc;

using AutoContext.Client.Core.Engine.Rpc;
using AutoContext.Client.Core.Tests.Support.Engine.Rpc;
using AutoContext.Client.Core.Tests.Support.Shared;
using AutoContext.Engine.Protocol.Messages.Logs;
using AutoContext.Engine.Protocol.Serialization;

public sealed class LogsRpcClientTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_connection()
        => Assert.Throws<ArgumentNullException>(() => new LogsRpcClient(connection: null!));

    [Fact]
    public async Task Should_marshal_the_bounds_on_get_engine()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var pair = await EnginePipePair.CreateAsync(cancellationToken);
        var client = new LogsRpcClient(pair.ClientConnection);

        // Act
        var call = client.GetEngineAsync(lastN: 25, since: null, cancellationToken);
        var request = await pair.ReadRequestAndRespondEmptyAsync(cancellationToken);
        await call;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(LogsMethods.GetEngine, request.Method),
            () => Assert.Equal(25, request.Params?.GetProperty("lastN").GetInt32()));
    }

    [Fact]
    public async Task Should_marshal_the_worker_id_and_return_not_found_on_get_worker()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var pair = await EnginePipePair.CreateAsync(cancellationToken);
        var client = new LogsRpcClient(pair.ClientConnection);

        // Act
        var call = client.GetWorkerAsync("workspace", lastN: null, since: null, cancellationToken);
        var request = await pair.ReadRequestAsync(cancellationToken);
        await pair.WriteResponseAsync(
            request.Id,
            JsonElementTestFactory.FromValue(
                new JsonLogsGetWorkerNotFoundResult { WorkerId = "workspace" },
                ProtocolJsonContext.Default.JsonLogsGetWorkerResult),
            cancellationToken);
        var result = await call;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(LogsMethods.GetWorker, request.Method),
            () => Assert.Equal("workspace", request.Params?.GetProperty("workerId").GetString()),
            () => Assert.IsType<JsonLogsGetWorkerNotFoundResult>(result));
    }
}
