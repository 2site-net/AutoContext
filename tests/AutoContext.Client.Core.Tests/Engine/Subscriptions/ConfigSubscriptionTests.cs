namespace AutoContext.Client.Core.Tests.Engine.Subscriptions;

using AutoContext.Client.Core.Engine.Subscriptions;
using AutoContext.Client.Core.Tests.Support.Engine.Subscriptions;
using AutoContext.Client.Core.Tests.Support.Shared;
using AutoContext.Engine.Protocol.Messages.Config;
using AutoContext.Engine.Protocol.Serialization;

public sealed class ConfigSubscriptionTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_connector()
        => Assert.Throws<ArgumentNullException>(() => new ConfigSubscription(connector: null!));

    [Fact]
    public async Task Should_yield_the_snapshot()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = SubscriptionTestContext.ForRpc(async (peer, ct) =>
        {
            var subscribe = await peer.ReadRequestAsync(ct);
            var frame = JsonElementTestFactory.FromValue(
                new JsonConfigSnapshotFrame(new JsonConfigSnapshot { Version = "9.9.9" }),
                ProtocolJsonContext.Default.JsonConfigStreamFrame);
            await peer.WriteStreamNextAsync(subscribe.Id, frame, ct);
        });
        var subscription = new ConfigSubscription(context.Connector);
        var received = new List<JsonConfigSnapshot>();

        // Act
        await foreach (var snapshot in subscription.SubscribeAsync(cancellationToken))
        {
            received.Add(snapshot);
            break;
        }

        // Assert
        Assert.Multiple(
            () => Assert.Single(received),
            () => Assert.Equal("9.9.9", received[0].Version));
    }

    [Fact]
    public async Task Should_throw_on_a_dropped_frame()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = SubscriptionTestContext.ForRpc(async (peer, ct) =>
        {
            var subscribe = await peer.ReadRequestAsync(ct);
            var frame = JsonElementTestFactory.FromValue(
                new JsonConfigDroppedFrame(JsonConfigDroppedFrame.SlowSubscriberReason),
                ProtocolJsonContext.Default.JsonConfigStreamFrame);
            await peer.WriteStreamNextAsync(subscribe.Id, frame, ct);
        });
        var subscription = new ConfigSubscription(context.Connector);

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
