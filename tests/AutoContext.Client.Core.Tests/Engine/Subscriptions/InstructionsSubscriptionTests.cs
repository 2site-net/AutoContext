namespace AutoContext.Client.Core.Tests.Engine.Subscriptions;

using AutoContext.Client.Core.Engine.Subscriptions;
using AutoContext.Client.Core.Tests.Support.Engine.Subscriptions;
using AutoContext.Client.Core.Tests.Support.Shared;
using AutoContext.Engine.Protocol.Messages.Instructions;
using AutoContext.Engine.Protocol.Serialization;

public sealed class InstructionsSubscriptionTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_connector()
        => Assert.Throws<ArgumentNullException>(() => new InstructionsSubscription(connector: null!));

    [Fact]
    public async Task Should_yield_the_snapshot_files()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = SubscriptionTestContext.ForRpc(async (peer, ct) =>
        {
            var subscribe = await peer.ReadRequestAsync(ct);
            var frame = JsonElementTestFactory.FromValue(
                new JsonInstructionsSnapshotFrame([]),
                ProtocolJsonContext.Default.JsonInstructionsStreamFrame);
            await peer.WriteStreamNextAsync(subscribe.Id, frame, ct);
        });
        var subscription = new InstructionsSubscription(context.Connector);
        var received = new List<IReadOnlyList<JsonInstructionsListRow>>();

        // Act
        await foreach (var files in subscription.SubscribeAsync(cancellationToken))
        {
            received.Add(files);
            break;
        }

        // Assert
        Assert.Multiple(
            () => Assert.Single(received),
            () => Assert.Empty(received[0]));
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
                new JsonInstructionsDroppedFrame(JsonInstructionsDroppedFrame.SlowSubscriberReason),
                ProtocolJsonContext.Default.JsonInstructionsStreamFrame);
            await peer.WriteStreamNextAsync(subscribe.Id, frame, ct);
        });
        var subscription = new InstructionsSubscription(context.Connector);

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
