namespace AutoContext.Client.Core.Tests.Engine.Rpc;

using AutoContext.Client.Core.Engine.Rpc;
using AutoContext.Client.Core.Tests.Support.Engine.Rpc;
using AutoContext.Client.Core.Tests.Support.Shared;
using AutoContext.Engine.Protocol.Messages.Config;
using AutoContext.Engine.Protocol.Serialization;

public sealed class ConfigRpcClientTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_connection()
        => Assert.Throws<ArgumentNullException>(() => new ConfigRpcClient(connection: null!));

    [Fact]
    public async Task Should_return_the_snapshot_on_get()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var pair = await EnginePipePair.CreateAsync(cancellationToken);
        var client = new ConfigRpcClient(pair.ClientConnection);

        // Act
        var call = client.GetAsync(cancellationToken);
        var request = await pair.ReadRequestAsync(cancellationToken);
        await pair.WriteResponseAsync(
            request.Id,
            JsonElementTestFactory.FromValue(
                new JsonConfigSnapshot { Version = "9.9.9" },
                ProtocolJsonContext.Default.JsonConfigSnapshot),
            cancellationToken);
        var result = await call;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(ConfigMethods.Get, request.Method),
            () => Assert.Equal("9.9.9", result.Version));
    }

    [Fact]
    public async Task Should_marshal_the_file_name_on_toggle_file()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var pair = await EnginePipePair.CreateAsync(cancellationToken);
        var client = new ConfigRpcClient(pair.ClientConnection);

        // Act
        var call = client.ToggleFileAsync("testing", cancellationToken);
        var request = await pair.ReadRequestAndRespondEmptyAsync(cancellationToken);
        await call;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(ConfigMethods.ToggleFile, request.Method),
            () => Assert.Equal("testing", request.Params?.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task Should_marshal_the_name_and_rule_on_toggle_rule()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var pair = await EnginePipePair.CreateAsync(cancellationToken);
        var client = new ConfigRpcClient(pair.ClientConnection);

        // Act
        var call = client.ToggleRuleAsync("testing", "INST0001", cancellationToken);
        var request = await pair.ReadRequestAndRespondEmptyAsync(cancellationToken);
        await call;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(ConfigMethods.ToggleRule, request.Method),
            () => Assert.Equal("testing", request.Params?.GetProperty("name").GetString()),
            () => Assert.Equal("INST0001", request.Params?.GetProperty("ruleId").GetString()));
    }
}
