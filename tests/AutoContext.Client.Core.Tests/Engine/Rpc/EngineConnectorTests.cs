namespace AutoContext.Client.Core.Tests.Engine.Rpc;

using AutoContext.Client.Core;
using AutoContext.Client.Core.Engine.Rpc;
using AutoContext.Client.Core.Tests.Support.Engine;
using AutoContext.Client.Core.Tests.Support.Engine.Rpc;
using AutoContext.Engine.Protocol;

public sealed class EngineConnectorTests
{
    [Fact]
    public async Task Should_handshake_and_exchange_on_a_warm_connect()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var workspacePath = ConnectorTestHarness.NewWorkspacePath();
        var instanceId = Guid.NewGuid();
        var address = ConnectorTestHarness.Address(EndpointKind.Rpc, workspacePath, instanceId);
        await using var server = new FakeEnginePipeServer(address, ProtocolVersion.Current);
        var spawner = new FakeEngineSpawner();
        var connector = ConnectorTestHarness.CreateConnector(
            new ClientOptions { WorkspacePath = workspacePath, InstanceId = instanceId, SpawnDisabled = true },
            spawner);

        // Act
        await using var connection = await connector.ConnectAsync(EndpointKind.Rpc, cancellationToken);
        var response = await connection.ExchangeAsync("Test.Echo", parameters: null, cancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(0, spawner.SpawnCount),
            () => Assert.Equal(1, server.HelloCount),
            () => Assert.Null(response.Error),
            () => Assert.Equal("Test.Echo", response.Result?.GetString()));
    }

    [Fact]
    public async Task Should_throw_without_spawning_when_spawn_disabled_and_no_engine()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var spawner = new FakeEngineSpawner();
        var connector = ConnectorTestHarness.CreateConnector(
            new ClientOptions
            {
                WorkspacePath = ConnectorTestHarness.NewWorkspacePath(),
                InstanceId = Guid.NewGuid(),
                SpawnDisabled = true,
            },
            spawner);

        // Act
        await Assert.ThrowsAsync<EngineUnavailableException>(
            () => connector.ConnectAsync(EndpointKind.Rpc, cancellationToken));

        // Assert
        Assert.Equal(0, spawner.SpawnCount);
    }

    [Fact]
    public async Task Should_spawn_then_connect_on_a_cold_start()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var workspacePath = ConnectorTestHarness.NewWorkspacePath();
        var instanceId = Guid.NewGuid();
        await using var engine = new ColdStartEngineTestHarness();
        var connector = ConnectorTestHarness.CreateConnector(
            new ClientOptions { WorkspacePath = workspacePath, InstanceId = instanceId },
            engine.Spawner);

        // Act
        await using var connection = await connector.ConnectAsync(EndpointKind.Rpc, cancellationToken);
        var response = await connection.ExchangeAsync("Test.Ping", parameters: null, cancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(1, engine.Spawner.SpawnCount),
            () => Assert.Equal(workspacePath, engine.Spawner.LastRequest?.WorkspacePath),
            () => Assert.Equal(instanceId, engine.Spawner.LastRequest?.InstanceId),
            () => Assert.Null(response.Error));
    }

    [Fact]
    public async Task Should_throw_on_a_protocol_version_mismatch_without_spawning()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var workspacePath = ConnectorTestHarness.NewWorkspacePath();
        var instanceId = Guid.NewGuid();
        var address = ConnectorTestHarness.Address(EndpointKind.Rpc, workspacePath, instanceId);
        await using var server = new FakeEnginePipeServer(address, ProtocolVersion.Current + 1);
        var spawner = new FakeEngineSpawner();
        var connector = ConnectorTestHarness.CreateConnector(
            new ClientOptions { WorkspacePath = workspacePath, InstanceId = instanceId },
            spawner);

        // Act
        await Assert.ThrowsAsync<EngineProtocolException>(
            () => connector.ConnectAsync(EndpointKind.Rpc, cancellationToken));

        // Assert
        Assert.Equal(0, spawner.SpawnCount);
    }

    [Fact]
    public async Task Should_connect_without_a_handshake_on_a_passive_endpoint()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var workspacePath = ConnectorTestHarness.NewWorkspacePath();
        var instanceId = Guid.NewGuid();
        var address = ConnectorTestHarness.Address(EndpointKind.Health, workspacePath, instanceId);
        await using var server = new FakeEnginePipeServer(address, ProtocolVersion.Current);
        var spawner = new FakeEngineSpawner();
        var connector = ConnectorTestHarness.CreateConnector(
            new ClientOptions { WorkspacePath = workspacePath, InstanceId = instanceId, SpawnDisabled = true },
            spawner);

        // Act
        await using var connection = await connector.ConnectAsync(EndpointKind.Health, cancellationToken);

        // Assert
        Assert.Equal(0, server.HelloCount);
    }
}
