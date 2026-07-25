namespace AutoContext.Client.Core.Tests;

using AutoContext.Client.Core.Engine.Rpc;
using AutoContext.Client.Core.Tests.Support;
using AutoContext.Engine.Protocol;

public sealed class EngineConnectorTests
{
    [Fact]
    public async Task Warm_connect_handshakes_and_exchanges()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workspacePath = ConnectorTestHarness.NewWorkspacePath();
        var instanceId = Guid.NewGuid();
        var address = ConnectorTestHarness.Address(EndpointKind.Rpc, workspacePath, instanceId);

        await using var server = new FakeEnginePipeServer(address, ProtocolVersion.Current);
        var spawner = new FakeEngineSpawner();
        var connector = ConnectorTestHarness.CreateConnector(
            new ClientOptions { WorkspacePath = workspacePath, InstanceId = instanceId, SpawnDisabled = true },
            spawner);

        await using var connection = await connector.ConnectAsync(EndpointKind.Rpc, cancellationToken);
        var response = await connection.ExchangeAsync("Test.Echo", parameters: null, cancellationToken);

        Assert.Multiple(
            () => Assert.Equal(0, spawner.SpawnCount),
            () => Assert.Equal(1, server.HelloCount),
            () => Assert.Null(response.Error),
            () => Assert.Equal("Test.Echo", response.Result?.GetString()));
    }

    [Fact]
    public async Task Spawn_disabled_with_no_engine_throws_without_spawning()
    {
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

        await Assert.ThrowsAsync<EngineUnavailableException>(
            () => connector.ConnectAsync(EndpointKind.Rpc, cancellationToken));
        Assert.Equal(0, spawner.SpawnCount);
    }

    [Fact]
    public async Task Find_or_spawn_spawns_then_connects()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workspacePath = ConnectorTestHarness.NewWorkspacePath();
        var instanceId = Guid.NewGuid();

        FakeEnginePipeServer? spawned = null;
        var spawner = new FakeEngineSpawner((request, _) =>
        {
            var address = ConnectorTestHarness.Address(
                EndpointKind.Rpc, request.WorkspacePath, request.InstanceId);
            spawned = new FakeEnginePipeServer(address, ProtocolVersion.Current);
            return Task.CompletedTask;
        });

        try
        {
            var connector = ConnectorTestHarness.CreateConnector(
                new ClientOptions { WorkspacePath = workspacePath, InstanceId = instanceId },
                spawner);

            await using var connection = await connector.ConnectAsync(EndpointKind.Rpc, cancellationToken);
            var response = await connection.ExchangeAsync("Test.Ping", parameters: null, cancellationToken);

            Assert.Multiple(
                () => Assert.Equal(1, spawner.SpawnCount),
                () => Assert.Equal(workspacePath, spawner.LastRequest?.WorkspacePath),
                () => Assert.Equal(instanceId, spawner.LastRequest?.InstanceId),
                () => Assert.Null(response.Error));
        }
        finally
        {
            if (spawned is not null)
            {
                await spawned.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task Protocol_version_mismatch_throws_without_spawning()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workspacePath = ConnectorTestHarness.NewWorkspacePath();
        var instanceId = Guid.NewGuid();
        var address = ConnectorTestHarness.Address(EndpointKind.Rpc, workspacePath, instanceId);

        await using var server = new FakeEnginePipeServer(address, ProtocolVersion.Current + 1);
        var spawner = new FakeEngineSpawner();
        var connector = ConnectorTestHarness.CreateConnector(
            new ClientOptions { WorkspacePath = workspacePath, InstanceId = instanceId },
            spawner);

        await Assert.ThrowsAsync<EngineProtocolException>(
            () => connector.ConnectAsync(EndpointKind.Rpc, cancellationToken));
        Assert.Equal(0, spawner.SpawnCount);
    }

    [Fact]
    public async Task Passive_endpoint_connects_without_a_handshake()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var workspacePath = ConnectorTestHarness.NewWorkspacePath();
        var instanceId = Guid.NewGuid();
        var address = ConnectorTestHarness.Address(EndpointKind.Health, workspacePath, instanceId);

        await using var server = new FakeEnginePipeServer(address, ProtocolVersion.Current);
        var spawner = new FakeEngineSpawner();
        var connector = ConnectorTestHarness.CreateConnector(
            new ClientOptions { WorkspacePath = workspacePath, InstanceId = instanceId, SpawnDisabled = true },
            spawner);

        await using var connection = await connector.ConnectAsync(EndpointKind.Health, cancellationToken);

        Assert.Equal(0, server.HelloCount);
    }
}
