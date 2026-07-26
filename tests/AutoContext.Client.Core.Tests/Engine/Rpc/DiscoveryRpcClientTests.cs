namespace AutoContext.Client.Core.Tests.Engine.Rpc;

using AutoContext.Client.Core.Engine.Rpc;
using AutoContext.Client.Core.Tests.Support.Engine.Rpc;
using AutoContext.Engine.Protocol.Messages.Discovery;

public sealed class DiscoveryRpcClientTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_connection()
        => Assert.Throws<ArgumentNullException>(() => new DiscoveryRpcClient(connection: null!));

    [Fact]
    public async Task Should_marshal_the_prompt_on_route_for_prompt()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var pair = await EnginePipePair.CreateAsync(cancellationToken);
        var client = new DiscoveryRpcClient(pair.ClientConnection);

        // Act
        var call = client.RouteForPromptAsync("port this to c#", cancellationToken);
        var request = await pair.ReadRequestAndRespondEmptyAsync(cancellationToken);
        await call;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(DiscoveryMethods.RouteForPrompt, request.Method),
            () => Assert.Equal("port this to c#", request.Params?.GetProperty("prompt").GetString()));
    }

    [Fact]
    public async Task Should_marshal_the_tool_name_on_route_for_tool()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var pair = await EnginePipePair.CreateAsync(cancellationToken);
        var client = new DiscoveryRpcClient(pair.ClientConnection);

        // Act
        var call = client.RouteForToolAsync("read_editorconfig", cancellationToken);
        var request = await pair.ReadRequestAndRespondEmptyAsync(cancellationToken);
        await call;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(DiscoveryMethods.RouteForTool, request.Method),
            () => Assert.Equal("read_editorconfig", request.Params?.GetProperty("name").GetString()));
    }
}
