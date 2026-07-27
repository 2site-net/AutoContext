namespace AutoContext.Client.Core.Tests.Engine.Rpc;

using AutoContext.Client.Core.Engine.Rpc;
using AutoContext.Client.Core.Tests.Support.Engine;
using AutoContext.Client.Core.Tests.Support.Engine.Rpc;
using AutoContext.Client.Core.Tests.Support.Shared;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Messages.Registry;
using AutoContext.Engine.Protocol.Serialization;

public sealed class EngineRpcClientTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_connection()
        => Assert.Throws<ArgumentNullException>(() => new EngineRpcClient(connection: null!));

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Should_read_the_live_registry_entry_from_an_in_process_engine()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var engine = await InProcessEngineTestHarness.StartAsync(cancellationToken);
        await using var client = await engine.ConnectAsync(cancellationToken);

        // Act
        var result = await client.Engine.RegistryEntriesAsync(cancellationToken);

        // Assert
        var entry = Assert.Single(result.Entries);
        Assert.Equal(engine.InstanceId, entry.InstanceId);
    }

    [Fact]
    public async Task Should_read_the_registry_entries()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var pair = await EnginePipePair.CreateAsync(cancellationToken);
        var client = new EngineRpcClient(pair.ClientConnection);

        // Act
        var call = client.RegistryEntriesAsync(cancellationToken);
        var request = await pair.ReadRequestAsync(cancellationToken);
        await pair.WriteResponseAsync(
            request.Id,
            JsonElementTestFactory.FromValue(
                new JsonRegistryEntriesResult(),
                ProtocolJsonContext.Default.JsonRegistryEntriesResult),
            cancellationToken);
        var result = await call;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(RegistryMethods.RegistryEntries, request.Method),
            () => Assert.Empty(result.Entries));
    }

    [Fact]
    public async Task Should_return_the_accepted_result_on_shutdown()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var pair = await EnginePipePair.CreateAsync(cancellationToken);
        var client = new EngineRpcClient(pair.ClientConnection);

        // Act
        var call = client.ShutdownAsync(cancellationToken);
        var request = await pair.ReadRequestAsync(cancellationToken);
        await pair.WriteResponseAsync(
            request.Id,
            JsonElementTestFactory.FromValue(
                new JsonShutdownResult { Accepted = true },
                ProtocolJsonContext.Default.JsonShutdownResult),
            cancellationToken);
        var result = await call;

        // Assert
        Assert.Multiple(
            () => Assert.Equal(ProtocolMethods.Shutdown, request.Method),
            () => Assert.True(result.Accepted));
    }
}
