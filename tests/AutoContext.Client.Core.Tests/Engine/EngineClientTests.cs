namespace AutoContext.Client.Core.Tests.Engine;

using AutoContext.Client.Core;
using AutoContext.Client.Core.Engine;
using AutoContext.Client.Core.Tests.Support.Engine;
using AutoContext.Client.Core.Tests.Support.Engine.Rpc;
using AutoContext.Engine.Protocol;

public sealed class EngineClientTests
{
    [Fact]
    public async Task Should_throw_when_connecting_with_a_null_connector()
        => await Assert.ThrowsAsync<ArgumentNullException>(
            () => EngineClient.ConnectAsync(connector: null!, TestContext.Current.CancellationToken));

    [Fact]
    public async Task Should_expose_the_typed_families_after_connect()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var workspacePath = ConnectorTestHarness.NewWorkspacePath();
        var instanceId = Guid.NewGuid();
        var address = ConnectorTestHarness.Address(EndpointKind.Rpc, workspacePath, instanceId);
        await using var server = new FakeEnginePipeServer(address, ProtocolVersion.Current);
        var connector = ConnectorTestHarness.CreateConnector(
            new ClientOptions { WorkspacePath = workspacePath, InstanceId = instanceId, SpawnDisabled = true },
            new FakeEngineSpawner());

        // Act
        await using var client = await EngineClient.ConnectAsync(connector, cancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(1, server.HelloCount),
            () => Assert.NotNull(client.Engine),
            () => Assert.NotNull(client.Config),
            () => Assert.NotNull(client.ConfigChanges()));
    }
}
