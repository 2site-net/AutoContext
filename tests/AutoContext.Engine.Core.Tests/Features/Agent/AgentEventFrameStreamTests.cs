namespace AutoContext.Engine.Core.Tests.Features.Agent;

using System.Threading.Channels;

using AutoContext.Engine.Core.Features.Agent;
using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Protocol.Messages.Agent;

public sealed class AgentEventFrameStreamTests
{
    [Fact]
    public async Task Should_yield_each_event_unchanged_in_order()
    {
        var channel = Channel.CreateUnbounded<JsonAgentEvent>();
        var first = new JsonAgentEvent { Kind = AgentEventKinds.SubagentStarted, SessionId = "s-1" };
        var second = new JsonAgentEvent { Kind = AgentEventKinds.TurnEnded, SessionId = "s-1" };

        Assert.True(channel.Writer.TryWrite(first));
        Assert.True(channel.Writer.TryWrite(second));
        channel.Writer.Complete();

        using var subscription = new BroadcasterSubscription<JsonAgentEvent>(
            channel.Reader,
            release: () => { },
            wasDropped: () => false);

        var frames = new List<JsonAgentEvent>();
        await foreach (var frame in new AgentEventFrameStream()
            .StreamAsync(subscription, TestContext.Current.CancellationToken))
        {
            frames.Add(frame);
        }

        Assert.Multiple(
            () => Assert.Equal(2, frames.Count),
            () => Assert.Same(first, frames[0]),
            () => Assert.Same(second, frames[1]));
    }

    [Fact]
    public async Task Should_not_yield_a_terminal_frame_when_not_dropped()
    {
        var channel = Channel.CreateUnbounded<JsonAgentEvent>();
        channel.Writer.Complete();

        using var subscription = new BroadcasterSubscription<JsonAgentEvent>(
            channel.Reader,
            release: () => { },
            wasDropped: () => false);

        var frames = new List<JsonAgentEvent>();
        await foreach (var frame in new AgentEventFrameStream()
            .StreamAsync(subscription, TestContext.Current.CancellationToken))
        {
            frames.Add(frame);
        }

        Assert.Empty(frames);
    }

    [Fact]
    public async Task Should_yield_a_terminal_dropped_frame_when_the_subscriber_was_dropped()
    {
        var channel = Channel.CreateUnbounded<JsonAgentEvent>();
        channel.Writer.Complete();

        using var subscription = new BroadcasterSubscription<JsonAgentEvent>(
            channel.Reader,
            release: () => { },
            wasDropped: () => true);

        var frames = new List<JsonAgentEvent>();
        await foreach (var frame in new AgentEventFrameStream()
            .StreamAsync(subscription, TestContext.Current.CancellationToken))
        {
            frames.Add(frame);
        }

        var dropped = Assert.Single(frames);
        Assert.Multiple(
            () => Assert.Equal(AgentEventKinds.Dropped, dropped.Kind),
            () => Assert.Equal("slow-subscriber", dropped.Reason));
    }
}
