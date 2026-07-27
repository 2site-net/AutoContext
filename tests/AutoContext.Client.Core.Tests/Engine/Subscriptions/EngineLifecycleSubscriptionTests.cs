namespace AutoContext.Client.Core.Tests.Engine.Subscriptions;

using AutoContext.Client.Core.Engine.Subscriptions;
using AutoContext.Client.Core.Tests.Support.Engine;
using AutoContext.Client.Core.Tests.Support.Engine.Subscriptions;
using AutoContext.Client.Core.Tests.Support.Shared;
using AutoContext.Engine.Protocol.Messages.Lifecycle;
using AutoContext.Engine.Protocol.Serialization;

public sealed class EngineLifecycleSubscriptionTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_connector()
        => Assert.Throws<ArgumentNullException>(() => new EngineLifecycleSubscription(connector: null!));
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Should_replay_the_started_event_from_an_in_process_engine()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var engine = await InProcessEngineTestHarness.StartAsync(cancellationToken);
        await using var client = await engine.ConnectAsync(cancellationToken);

        // Act
        JsonLifecycleEvent? seed = null;
        await foreach (var lifecycleEvent in client.LifecycleEvents().SubscribeAsync(cancellationToken))
        {
            seed = lifecycleEvent;
            break;
        }

        // Assert
        Assert.NotNull(seed);
        Assert.Equal(engine.InstanceId, seed.InstanceId);
    }
    [Fact]
    public async Task Should_yield_each_pushed_lifecycle_event()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = SubscriptionTestContext.ForEvents(async (peer, ct) =>
        {
            var payload = JsonElementTestFactory.FromValue(
                new JsonLifecycleEvent { Kind = LifecycleEventKinds.Started, InstanceId = Guid.NewGuid() },
                ProtocolJsonContext.Default.JsonLifecycleEvent);
            await peer.WriteNotificationAsync(LifecycleMethods.Notification, payload, ct);
        });
        var subscription = new EngineLifecycleSubscription(context.Connector);
        var received = new List<JsonLifecycleEvent>();

        // Act
        await foreach (var lifecycleEvent in subscription.SubscribeAsync(cancellationToken))
        {
            received.Add(lifecycleEvent);
            break;
        }

        // Assert
        Assert.Multiple(
            () => Assert.Single(received),
            () => Assert.Equal(LifecycleEventKinds.Started, received[0].Kind));
    }

    [Fact]
    public async Task Should_throw_on_a_dropped_event()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = SubscriptionTestContext.ForEvents(async (peer, ct) =>
        {
            var payload = JsonElementTestFactory.FromValue(
                new JsonLifecycleEvent { Kind = LifecycleEventKinds.Dropped, Reason = "slow-subscriber" },
                ProtocolJsonContext.Default.JsonLifecycleEvent);
            await peer.WriteNotificationAsync(LifecycleMethods.Notification, payload, ct);
        });
        var subscription = new EngineLifecycleSubscription(context.Connector);

        // Act + Assert
        await Assert.ThrowsAsync<EngineSubscriptionDroppedException>(
            async () =>
            {
                await foreach (var _ in subscription.SubscribeAsync(cancellationToken))
                {
                }
            });
    }
}
