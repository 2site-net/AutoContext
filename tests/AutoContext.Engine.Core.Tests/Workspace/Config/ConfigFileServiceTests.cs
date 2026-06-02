namespace AutoContext.Engine.Core.Tests.Workspace.Config;

using AutoContext.Engine.Core.Tests.Support.Infrastructure.Events;
using AutoContext.Engine.Core.Tests.Support.Shared;
using AutoContext.Engine.Core.Tests.Support.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;
using AutoContext.Engine.Protocol.Messages.Config;

public sealed class ConfigFileServiceTests(TempDirectoryFixture tempDirectory)
    : IClassFixture<TempDirectoryFixture>
{
    [Fact]
    public void Should_throw_when_constructed_with_null_manager()
    {
        // Arrange
        var broadcaster = SnapshotBroadcasterTestFactory.Create<JsonConfigSnapshot>("Config.Subscribe");

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new ConfigFileService(null!, broadcaster));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_broadcaster()
    {
        // Arrange
        using var manager = ConfigFileManagerTestFactory.Create(tempDirectory.CreateDirectory());

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new ConfigFileService(manager, null!));
    }

    [Fact]
    public async Task Should_prime_broadcaster_with_loaded_snapshot_on_start()
    {
        // Arrange
        using var manager = ConfigFileManagerTestFactory.Create(tempDirectory.CreateDirectory());
        var broadcaster = SnapshotBroadcasterTestFactory.Create<JsonConfigSnapshot>("Config.Subscribe");
        var service = new ConfigFileService(manager, broadcaster);

        // Act — start loads + primes, then a fresh subscriber sees
        // the loaded snapshot replayed as its first frame.
        await service.StartAsync(TestContext.Current.CancellationToken);
        using var subscription = broadcaster.Subscribe();
        await service.StopAsync(TestContext.Current.CancellationToken);
        var frames = await ConfigStreamTestDrainer.DrainAsync(subscription);

        // Assert — empty workspace yields a versionless seed.
        var seed = Assert.IsType<JsonConfigSnapshotFrame>(Assert.Single(frames));
        Assert.Null(seed.Snapshot.Version);
    }

    [Fact]
    public async Task Should_publish_snapshot_when_manager_raises_change()
    {
        // Arrange
        using var manager = ConfigFileManagerTestFactory.Create(tempDirectory.CreateDirectory());
        var broadcaster = SnapshotBroadcasterTestFactory.Create<JsonConfigSnapshot>("Config.Subscribe");
        var service = new ConfigFileService(manager, broadcaster);
        await service.StartAsync(TestContext.Current.CancellationToken);
        using var subscription = broadcaster.Subscribe();

        // Act — an edit through the manager raises Changed, which
        // the bridge fans out to the live subscriber.
        await manager.UpdateAsync(
            config => config with
            {
                McpTools = [new ConfigMcpTool { Name = "t1", Disabled = true }],
            },
            TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);
        var frames = await ConfigStreamTestDrainer.DrainAsync(subscription);

        // Assert — the seed frame followed by the edited snapshot,
        // stamped with the engine version on write.
        Assert.Equal(2, frames.Count);
        var published = Assert.IsType<JsonConfigSnapshotFrame>(frames[1]);
        Assert.Multiple(
            () => Assert.Equal(ConfigFileManagerTestFactory.EngineVersion, published.Snapshot.Version),
            () => Assert.Equal("t1", Assert.Single(published.Snapshot.McpTools).Name));
    }

    [Fact]
    public async Task Should_complete_broadcaster_on_stop()
    {
        // Arrange
        using var manager = ConfigFileManagerTestFactory.Create(tempDirectory.CreateDirectory());
        var broadcaster = SnapshotBroadcasterTestFactory.Create<JsonConfigSnapshot>("Config.Subscribe");
        var service = new ConfigFileService(manager, broadcaster);
        await service.StartAsync(TestContext.Current.CancellationToken);
        using var subscription = broadcaster.Subscribe();

        // Act
        await service.StopAsync(TestContext.Current.CancellationToken);
        var frames = await ConfigStreamTestDrainer.DrainAsync(subscription);

        // Assert — the stream terminates cleanly: just the seed
        // frame and EOF, no terminal evicted frame.
        Assert.IsType<JsonConfigSnapshotFrame>(Assert.Single(frames));
    }
}
