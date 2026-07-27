namespace AutoContext.Client.Core.Tests.Engine.Rpc;

using AutoContext.Client.Core.Engine.Rpc;
using AutoContext.Client.Core.Tests.Support.Engine;
using AutoContext.Client.Core.Tests.Support.Engine.Rpc;
using AutoContext.Client.Core.Tests.Support.Shared;
using AutoContext.Engine.Protocol.Messages.McpTools;
using AutoContext.Engine.Protocol.Serialization;
using AutoContext.Engine.Tests.Support.Mcp;

public sealed class McpToolsRpcClientTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_connection()
        => Assert.Throws<ArgumentNullException>(() => new McpToolsRpcClient(connection: null!));

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Should_list_tools_and_reject_an_unknown_tool_on_an_in_process_engine()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var engine = await InProcessEngineTestHarness.StartAsync(cancellationToken);
        await using var client = await engine.ConnectAsync(cancellationToken);

        // Act
        var listed = await client.McpTools.ListAsync(cancellationToken);
        var missing = await client.McpTools.InvokeAsync("no_such_tool", arguments: null, cancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.NotEmpty(listed.Tools),
            () => Assert.IsType<JsonMcpToolsInvokeNotFoundResult>(missing));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Should_dispatch_a_tool_call_to_a_worker_spawned_by_an_in_process_engine()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var engine = await InProcessEngineTestHarness.StartAsync(
            cancellationToken, withTestDriverWorker: true);
        await using var client = await engine.ConnectAsync(cancellationToken);
        var arguments = JsonElementTestFactory.Parse("{\"payload\":\"ping\"}");

        // Act
        var result = await client.McpTools.InvokeAsync(
            TestDriverResourcesOverlay.EchoTool, arguments, cancellationToken);

        // Assert
        var ok = Assert.IsType<JsonMcpToolsInvokeOkResult>(result);
        Assert.NotEmpty(ok.Content);
    }

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
