namespace AutoContext.Engine.Core.Tests.Features.Instructions;

using AutoContext.Engine.Core.Features.Instructions;
using AutoContext.Engine.Core.Tests.Support.Features.Instructions;
using AutoContext.Engine.Core.Tests.Support.Lifecycle;
using AutoContext.Engine.Core.Tests.Support.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;
using AutoContext.Engine.Protocol.Messages.Instructions;

public sealed class InstructionsSubscriptionServiceTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_projector()
    {
        // Arrange
        var broadcaster = LifecycleServiceFixture.CreateInstructionsBroadcaster();
        var configChanges = LifecycleServiceFixture.CreateConfigChangeNotifier();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new InstructionsSubscriptionService(null!, broadcaster, configChanges));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_broadcaster()
    {
        // Arrange
        var projector = LifecycleServiceFixture.CreateInstructionsListProjector();
        var configChanges = LifecycleServiceFixture.CreateConfigChangeNotifier();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new InstructionsSubscriptionService(projector, null!, configChanges));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_config_change_notifier()
    {
        // Arrange
        var projector = LifecycleServiceFixture.CreateInstructionsListProjector();
        var broadcaster = LifecycleServiceFixture.CreateInstructionsBroadcaster();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new InstructionsSubscriptionService(projector, broadcaster, null!));
    }

    [Fact]
    public async Task Should_prime_broadcaster_with_listing_on_start()
    {
        // Arrange
        var manifest = new FakeInstructionsManifestAccessor(
            InstructionsFileManifestEntryTestFactory.Create("testing"),
            InstructionsFileManifestEntryTestFactory.Create("design"));
        var projector = LifecycleServiceFixture.CreateInstructionsListProjector(manifest: manifest);
        var broadcaster = LifecycleServiceFixture.CreateInstructionsBroadcaster();
        var configChanges = LifecycleServiceFixture.CreateConfigChangeNotifier();
        var service = new InstructionsSubscriptionService(projector, broadcaster, configChanges);

        // Act — start primes, then a fresh subscriber sees the
        // projected listing replayed as its first frame.
        await service.StartAsync(TestContext.Current.CancellationToken);
        using var subscription = broadcaster.Subscribe();
        await service.StopAsync(TestContext.Current.CancellationToken);
        var frames = await InstructionsStreamTestDrainer.DrainAsync(subscription);

        // Assert
        var seed = Assert.IsType<JsonInstructionsSnapshotFrame>(Assert.Single(frames));
        Assert.Multiple(
            () => Assert.Equal(2, seed.Files.Count),
            () => Assert.Equal("testing", seed.Files[0].Key));
    }

    [Fact]
    public async Task Should_complete_broadcaster_on_stop()
    {
        // Arrange
        var projector = LifecycleServiceFixture.CreateInstructionsListProjector();
        var broadcaster = LifecycleServiceFixture.CreateInstructionsBroadcaster();
        var configChanges = LifecycleServiceFixture.CreateConfigChangeNotifier();
        var service = new InstructionsSubscriptionService(projector, broadcaster, configChanges);
        await service.StartAsync(TestContext.Current.CancellationToken);
        using var subscription = broadcaster.Subscribe();

        // Act
        await service.StopAsync(TestContext.Current.CancellationToken);
        var frames = await InstructionsStreamTestDrainer.DrainAsync(subscription);

        // Assert — the stream terminates cleanly: just the seed frame
        // and EOF, no terminal dropped frame.
        Assert.IsType<JsonInstructionsSnapshotFrame>(Assert.Single(frames));
    }

    [Fact]
    public async Task Should_rebroadcast_listing_with_reevaluated_disabled_flag_on_config_change()
    {
        // Arrange — one file, initially enabled, sharing one config
        // double as both the projector's read seam and the change
        // notifier the bridge subscribes to.
        var manifest = new FakeInstructionsManifestAccessor(
            InstructionsFileManifestEntryTestFactory.Create("testing"));
        var config = new FakeConfigSnapshotAccessor();
        var projector = LifecycleServiceFixture.CreateInstructionsListProjector(
            manifest: manifest, config: config);
        var broadcaster = LifecycleServiceFixture.CreateInstructionsBroadcaster();
        var service = new InstructionsSubscriptionService(projector, broadcaster, config);
        await service.StartAsync(TestContext.Current.CancellationToken);
        using var subscription = broadcaster.Subscribe();

        // Act — toggle the file disabled; the bridge re-projects and
        // republishes the listing without a corpus reload.
        await config.UpdateAsync(
            snapshot => snapshot with
            {
                Instructions = [new ConfigInstructionsFile { Name = "testing", Disabled = true }],
            },
            TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);
        var frames = await InstructionsStreamTestDrainer.DrainAsync(subscription);

        // Assert — the seed frame shows the file enabled, the
        // rebroadcast frame shows the re-evaluated disabled flag.
        Assert.Equal(2, frames.Count);
        var seed = Assert.IsType<JsonInstructionsSnapshotFrame>(frames[0]);
        var republished = Assert.IsType<JsonInstructionsSnapshotFrame>(frames[1]);
        Assert.Multiple(
            () => Assert.False(Assert.Single(seed.Files).Disabled),
            () => Assert.True(Assert.Single(republished.Files).Disabled));
    }
}
