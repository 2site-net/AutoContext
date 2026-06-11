namespace AutoContext.Engine.Core.Tests.Features.Instructions;

using AutoContext.Engine.Core.Features.Instructions;
using AutoContext.Engine.Core.Tests.Support.Features.Instructions;
using AutoContext.Engine.Core.Tests.Support.Lifecycle;
using AutoContext.Engine.Protocol.Messages.Instructions;

public sealed class InstructionsSubscriptionServiceTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_projector()
    {
        // Arrange
        var broadcaster = LifecycleServiceFixture.CreateInstructionsBroadcaster();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new InstructionsSubscriptionService(null!, broadcaster));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_broadcaster()
    {
        // Arrange
        var projector = LifecycleServiceFixture.CreateInstructionsListProjector();

        // Act + Assert
        Assert.Throws<ArgumentNullException>(
            () => new InstructionsSubscriptionService(projector, null!));
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
        var service = new InstructionsSubscriptionService(projector, broadcaster);

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
        var service = new InstructionsSubscriptionService(projector, broadcaster);
        await service.StartAsync(TestContext.Current.CancellationToken);
        using var subscription = broadcaster.Subscribe();

        // Act
        await service.StopAsync(TestContext.Current.CancellationToken);
        var frames = await InstructionsStreamTestDrainer.DrainAsync(subscription);

        // Assert — the stream terminates cleanly: just the seed frame
        // and EOF, no terminal dropped frame.
        Assert.IsType<JsonInstructionsSnapshotFrame>(Assert.Single(frames));
    }
}
