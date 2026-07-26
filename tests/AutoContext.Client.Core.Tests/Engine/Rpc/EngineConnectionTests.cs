namespace AutoContext.Client.Core.Tests.Engine.Rpc;

using System.Text.Json;

using AutoContext.Client.Core.Engine.Rpc;
using AutoContext.Client.Core.Tests.Support.Engine.Rpc;
using AutoContext.Client.Core.Tests.Support.Shared;
using AutoContext.Engine.Protocol.Messages.Lifecycle;
using AutoContext.Engine.Protocol.Serialization;

public sealed class EngineConnectionTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_stream()
        => Assert.Throws<ArgumentNullException>(() => new EngineConnection(stream: null!));

    [Fact]
    public async Task Should_return_the_deserialized_success_result()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var pair = await EnginePipePair.CreateAsync(cancellationToken);

        // Act
        var invoke = pair.ClientConnection.InvokeAsync(
            "Test.Method",
            parameters: null,
            ProtocolJsonContext.Default.JsonWorkspaceInfoResult,
            cancellationToken);
        var request = await pair.ReadRequestAsync(cancellationToken);
        await pair.WriteResponseAsync(
            request.Id, JsonElementTestFactory.Parse("{\"engineVersion\":\"1.2.3\"}"), cancellationToken);
        var result = await invoke;

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Test.Method", request.Method),
            () => Assert.Equal("1.2.3", result.EngineVersion));
    }

    [Fact]
    public async Task Should_map_an_error_response_to_EngineRpcException()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var pair = await EnginePipePair.CreateAsync(cancellationToken);

        // Act
        var invoke = pair.ClientConnection.InvokeAsync(
            "Test.Method",
            parameters: null,
            ProtocolJsonContext.Default.JsonWorkspaceInfoResult,
            cancellationToken);
        var request = await pair.ReadRequestAsync(cancellationToken);
        await pair.WriteErrorAsync(request.Id, -32000, "boom", cancellationToken);
        var exception = await Assert.ThrowsAsync<EngineRpcException>(() => invoke);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Test.Method", exception.Method),
            () => Assert.Equal(-32000, exception.ErrorCode));
    }

    [Fact]
    public async Task Should_throw_when_the_success_response_carries_no_result()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var pair = await EnginePipePair.CreateAsync(cancellationToken);

        // Act
        var invoke = pair.ClientConnection.InvokeAsync(
            "Test.Method",
            parameters: null,
            ProtocolJsonContext.Default.JsonWorkspaceInfoResult,
            cancellationToken);
        var request = await pair.ReadRequestAsync(cancellationToken);
        await pair.WriteResponseAsync(request.Id, result: null, cancellationToken);
        var exception = await Assert.ThrowsAsync<EngineRpcException>(() => invoke);

        // Assert
        Assert.Null(exception.ErrorCode);
    }

    [Fact]
    public async Task Should_throw_when_the_result_is_json_null()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var pair = await EnginePipePair.CreateAsync(cancellationToken);

        // Act
        var invoke = pair.ClientConnection.InvokeAsync(
            "Test.Method",
            parameters: null,
            ProtocolJsonContext.Default.JsonWorkspaceInfoResult,
            cancellationToken);
        var request = await pair.ReadRequestAsync(cancellationToken);
        await pair.WriteResponseAsync(request.Id, JsonElementTestFactory.Parse("null"), cancellationToken);

        // Assert
        await Assert.ThrowsAsync<EngineRpcException>(() => invoke);
    }

    [Fact]
    public async Task Should_yield_each_next_payload_until_complete()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var pair = await EnginePipePair.CreateAsync(cancellationToken);
        var received = new List<int>();

        // Act
        var consume = Task.Run(
            async () =>
            {
                await foreach (var element in pair.ClientConnection
                    .SubscribeAsync("Test.Subscribe", parameters: null, cancellationToken)
                    .WithCancellation(cancellationToken))
                {
                    received.Add(element.GetProperty("n").GetInt32());
                }
            },
            cancellationToken);
        var request = await pair.ReadRequestAsync(cancellationToken);
        await pair.WriteStreamNextAsync(request.Id, JsonElementTestFactory.Parse("{\"n\":1}"), cancellationToken);
        await pair.WriteStreamNextAsync(request.Id, JsonElementTestFactory.Parse("{\"n\":2}"), cancellationToken);
        await pair.WriteStreamCompleteAsync(request.Id, cancellationToken);
        await consume;

        // Assert
        Assert.Equal([1, 2], received);
    }

    [Fact]
    public async Task Should_map_a_stream_error_frame_to_EngineRpcException()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var pair = await EnginePipePair.CreateAsync(cancellationToken);

        // Act
        var consume = Task.Run(
            async () =>
            {
                await foreach (var _ in pair.ClientConnection
                    .SubscribeAsync("Test.Subscribe", parameters: null, cancellationToken)
                    .WithCancellation(cancellationToken))
                {
                }
            },
            cancellationToken);
        var request = await pair.ReadRequestAsync(cancellationToken);
        await pair.WriteStreamErrorAsync(request.Id, -32001, "stream boom", cancellationToken);
        var exception = await Assert.ThrowsAsync<EngineRpcException>(() => consume);

        // Assert
        Assert.Equal(-32001, exception.ErrorCode);
    }

    [Fact]
    public async Task Should_write_an_id_less_notification_frame_and_await_no_response()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var pair = await EnginePipePair.CreateAsync(cancellationToken);

        // Act
        var send = pair.ClientConnection.SendNotificationAsync(
            "Agent.TurnEnded", JsonElementTestFactory.Parse("{\"sessionId\":\"s1\"}"), cancellationToken);
        var frame = await pair.ReadRequestAsync(cancellationToken);
        await send;

        // Assert
        Assert.Multiple(
            () => Assert.Equal("Agent.TurnEnded", frame.Method),
            () => Assert.Equal(JsonValueKind.Undefined, frame.Id.ValueKind),
            () => Assert.Equal("s1", frame.Params?.GetProperty("sessionId").GetString()));
    }

    [Fact]
    public async Task Should_yield_each_pushed_notification()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var pair = await EnginePipePair.CreateAsync(cancellationToken);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var received = new List<JsonLifecycleEvent>();

        // Act
        var consume = Task.Run(
            async () =>
            {
                await foreach (var notification in pair.ClientConnection
                    .ReceiveNotificationsAsync(stop.Token))
                {
                    var payload = notification.Params!.Value.Deserialize(
                        ProtocolJsonContext.Default.JsonLifecycleEvent)!;
                    received.Add(payload);
                    if (received.Count == 2)
                    {
                        await stop.CancelAsync();
                    }
                }
            },
            cancellationToken);
        await pair.WriteNotificationAsync(
            LifecycleMethods.Notification, JsonElementTestFactory.Parse("{\"kind\":\"started\"}"), cancellationToken);
        await pair.WriteNotificationAsync(
            LifecycleMethods.Notification, JsonElementTestFactory.Parse("{\"kind\":\"reloading\"}"), cancellationToken);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => consume);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(2, received.Count),
            () => Assert.Equal(LifecycleEventKinds.Started, received[0].Kind),
            () => Assert.Equal(LifecycleEventKinds.Reloading, received[1].Kind));
    }
}
