namespace AutoContext.Client.Core.Tests.Engine.Rpc;

using System.Text.Json;

using AutoContext.Client.Core.Engine.Rpc;
using AutoContext.Client.Core.Tests.Support.Engine.Rpc;
using AutoContext.Engine.Protocol.Messages.Agent;

public sealed class AgentRpcClientTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_connection()
        => Assert.Throws<ArgumentNullException>(() => new AgentRpcClient(connection: null!));

    [Fact]
    public async Task Should_send_a_turn_ended_notification_with_the_session()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var pair = await EnginePipePair.CreateAsync(cancellationToken);
        var client = new AgentRpcClient(pair.ClientConnection);

        // Act
        var send = client.TurnEndedAsync("session-1", cancellationToken);
        var frame = await pair.ReadRequestAsync(cancellationToken);
        await send;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(AgentMethods.TurnEnded, frame.Method),
            () => Assert.Equal(JsonValueKind.Undefined, frame.Id.ValueKind),
            () => Assert.Equal("session-1", frame.Params?.GetProperty("sessionId").GetString()));
    }

    [Fact]
    public async Task Should_marshal_every_field_on_tool_used()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var pair = await EnginePipePair.CreateAsync(cancellationToken);
        var client = new AgentRpcClient(pair.ClientConnection);

        // Act
        var send = client.ToolUsedAsync("session-1", "read_editorconfig", "ok", cancellationToken);
        var frame = await pair.ReadRequestAsync(cancellationToken);
        await send;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(AgentMethods.ToolUsed, frame.Method),
            () => Assert.Equal("session-1", frame.Params?.GetProperty("sessionId").GetString()),
            () => Assert.Equal("read_editorconfig", frame.Params?.GetProperty("toolName").GetString()),
            () => Assert.Equal("ok", frame.Params?.GetProperty("outcome").GetString()));
    }
}
