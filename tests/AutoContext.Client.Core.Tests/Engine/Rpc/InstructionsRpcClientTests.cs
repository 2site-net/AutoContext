namespace AutoContext.Client.Core.Tests.Engine.Rpc;

using AutoContext.Client.Core.Engine.Rpc;
using AutoContext.Client.Core.Tests.Support.Engine.Rpc;
using AutoContext.Client.Core.Tests.Support.Shared;
using AutoContext.Engine.Protocol.Messages.Instructions;
using AutoContext.Engine.Protocol.Serialization;

public sealed class InstructionsRpcClientTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_connection()
        => Assert.Throws<ArgumentNullException>(() => new InstructionsRpcClient(connection: null!));

    [Fact]
    public async Task Should_send_the_list_method()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var pair = await EnginePipePair.CreateAsync(cancellationToken);
        var client = new InstructionsRpcClient(pair.ClientConnection);

        // Act
        var call = client.ListAsync(
            includeSections: false, applyToWorkspaceFilter: true, applyToHint: null, cancellationToken);
        var request = await pair.ReadRequestAndRespondEmptyAsync(cancellationToken);
        await call;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(InstructionsMethods.List, request.Method),
            () => Assert.False(request.Params?.GetProperty("includeSections").GetBoolean()));
    }

    [Fact]
    public async Task Should_send_the_categories_method()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var pair = await EnginePipePair.CreateAsync(cancellationToken);
        var client = new InstructionsRpcClient(pair.ClientConnection);

        // Act
        var call = client.CategoriesAsync(cancellationToken);
        var request = await pair.ReadRequestAndRespondEmptyAsync(cancellationToken);
        await call;

        // Assert
        Assert.Equal(InstructionsMethods.Categories, request.Method);
    }

    [Fact]
    public async Task Should_marshal_the_name_and_return_the_disabled_arm_on_get()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var pair = await EnginePipePair.CreateAsync(cancellationToken);
        var client = new InstructionsRpcClient(pair.ClientConnection);

        // Act
        var call = client.GetAsync("testing", sections: null, cancellationToken);
        var request = await pair.ReadRequestAsync(cancellationToken);
        await pair.WriteResponseAsync(
            request.Id,
            JsonElementTestFactory.FromValue(
                new JsonInstructionsGetDisabledResult { Name = "testing" },
                ProtocolJsonContext.Default.JsonInstructionsGetResult),
            cancellationToken);
        var result = await call;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(InstructionsMethods.Get, request.Method),
            () => Assert.Equal("testing", request.Params?.GetProperty("name").GetString()),
            () => Assert.IsType<JsonInstructionsGetDisabledResult>(result));
    }

    [Fact]
    public async Task Should_marshal_the_source_and_return_not_found_on_get_raw()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var pair = await EnginePipePair.CreateAsync(cancellationToken);
        var client = new InstructionsRpcClient(pair.ClientConnection);

        // Act
        var call = client.GetRawAsync("testing", InstructionsRawSource.Override, cancellationToken);
        var request = await pair.ReadRequestAsync(cancellationToken);
        await pair.WriteResponseAsync(
            request.Id,
            JsonElementTestFactory.FromValue(
                new JsonInstructionsGetRawNotFoundResult { Name = "testing" },
                ProtocolJsonContext.Default.JsonInstructionsGetRawResult),
            cancellationToken);
        var result = await call;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(InstructionsMethods.GetRaw, request.Method),
            () => Assert.Equal("override", request.Params?.GetProperty("source").GetString()),
            () => Assert.IsType<JsonInstructionsGetRawNotFoundResult>(result));
    }

    [Fact]
    public async Task Should_marshal_the_query_on_search_content()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var pair = await EnginePipePair.CreateAsync(cancellationToken);
        var client = new InstructionsRpcClient(pair.ClientConnection);

        // Act
        var call = client.SearchContentAsync("cancellation", limit: 5, includeDisabled: null, cancellationToken);
        var request = await pair.ReadRequestAndRespondEmptyAsync(cancellationToken);
        await call;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(InstructionsMethods.SearchContent, request.Method),
            () => Assert.Equal("cancellation", request.Params?.GetProperty("query").GetString()),
            () => Assert.Equal(5, request.Params?.GetProperty("limit").GetInt32()));
    }

    [Fact]
    public async Task Should_return_the_ok_arm_on_search_by_metadata()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var pair = await EnginePipePair.CreateAsync(cancellationToken);
        var client = new InstructionsRpcClient(pair.ClientConnection);

        // Act
        var call = client.SearchByMetadataAsync(predicate: null, includeSections: null, cancellationToken);
        var request = await pair.ReadRequestAsync(cancellationToken);
        await pair.WriteResponseAsync(
            request.Id,
            JsonElementTestFactory.FromValue(
                new JsonInstructionsSearchByMetadataOkResult(),
                ProtocolJsonContext.Default.JsonInstructionsSearchByMetadataResult),
            cancellationToken);
        var result = await call;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(InstructionsMethods.SearchByMetadata, request.Method),
            () => Assert.IsType<JsonInstructionsSearchByMetadataOkResult>(result));
    }
}
