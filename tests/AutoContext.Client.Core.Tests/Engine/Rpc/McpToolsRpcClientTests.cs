namespace AutoContext.Client.Core.Tests.Engine.Rpc;

using AutoContext.Client.Core.Engine.Rpc;
using AutoContext.Client.Core.Tests.Support.Engine.Rpc;
using AutoContext.Client.Core.Tests.Support.Shared;
using AutoContext.Engine.Protocol.Messages.McpTools;
using AutoContext.Engine.Protocol.Serialization;

public sealed class McpToolsRpcClientTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_connection()
        => Assert.Throws<ArgumentNullException>(() => new McpToolsRpcClient(connection: null!));

    [Fact]
    public async Task Should_send_the_list_method()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var pair = await EnginePipePair.CreateAsync(cancellationToken);
        var client = new McpToolsRpcClient(pair.ClientConnection);

        // Act
        var call = client.ListAsync(cancellationToken);
        var request = await pair.ReadRequestAndRespondEmptyAsync(cancellationToken);
        await call;

        // Assert
        Assert.Equal(McpToolsMethods.List, request.Method);
    }

    [Fact]
    public async Task Should_marshal_the_name_and_return_the_discriminated_arm_on_invoke()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var pair = await EnginePipePair.CreateAsync(cancellationToken);
        var client = new McpToolsRpcClient(pair.ClientConnection);

        // Act
        var call = client.InvokeAsync("analyze_csharp_code", arguments: null, cancellationToken);
        var request = await pair.ReadRequestAsync(cancellationToken);
        await pair.WriteResponseAsync(
            request.Id,
            JsonElementTestFactory.FromValue(
                new JsonMcpToolsInvokeNotFoundResult { Name = "analyze_csharp_code" },
                ProtocolJsonContext.Default.JsonMcpToolsInvokeResult),
            cancellationToken);
        var result = await call;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(McpToolsMethods.Invoke, request.Method),
            () => Assert.Equal("analyze_csharp_code", request.Params?.GetProperty("name").GetString()),
            () => Assert.IsType<JsonMcpToolsInvokeNotFoundResult>(result));
    }
}
