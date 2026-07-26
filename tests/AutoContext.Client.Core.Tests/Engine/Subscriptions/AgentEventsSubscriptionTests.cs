namespace AutoContext.Client.Core.Tests.Engine.Subscriptions;

using System.Text.Json;

using AutoContext.Client.Core.Engine.Subscriptions;
using AutoContext.Client.Core.Tests.Support.Engine.Subscriptions;
using AutoContext.Engine.Protocol.Messages.Agent;
using AutoContext.Engine.Protocol.Serialization;

public sealed class AgentEventsSubscriptionTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_connector()
        => Assert.Throws<ArgumentNullException>(() => new AgentEventsSubscription(connector: null!));

    [Fact]
    public async Task Should_yield_each_event()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = SubscriptionTestContext.ForRpc(async (peer, ct) =>
        {
            var subscribe = await peer.ReadRequestAsync(ct);
            var frame = JsonSerializer.SerializeToElement(
                new JsonAgentEvent { Kind = AgentEventKinds.TurnEnded, SessionId = "session-1" },
                ProtocolJsonContext.Default.JsonAgentEvent);
            await peer.WriteStreamNextAsync(subscribe.Id, frame, ct);
        });
        var subscription = new AgentEventsSubscription(context.Connector);
        var received = new List<JsonAgentEvent>();

        // Act
        await foreach (var agentEvent in subscription.SubscribeAsync(cancellationToken))
        {
            received.Add(agentEvent);
            break;
        }

        // Assert
        Assert.Multiple(
            () => Assert.Single(received),
            () => Assert.Equal(AgentEventKinds.TurnEnded, received[0].Kind));
    }

    [Fact]
    public async Task Should_throw_on_a_dropped_event()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = SubscriptionTestContext.ForRpc(async (peer, ct) =>
        {
            var subscribe = await peer.ReadRequestAsync(ct);
            var frame = JsonSerializer.SerializeToElement(
                new JsonAgentEvent { Kind = AgentEventKinds.Dropped, Reason = "slow-subscriber" },
                ProtocolJsonContext.Default.JsonAgentEvent);
            await peer.WriteStreamNextAsync(subscribe.Id, frame, ct);
        });
        var subscription = new AgentEventsSubscription(context.Connector);

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
