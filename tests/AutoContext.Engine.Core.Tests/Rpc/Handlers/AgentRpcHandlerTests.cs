namespace AutoContext.Engine.Core.Tests.Rpc.Handlers;

using System.Text.Json;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Core.Rpc.Handlers;
using AutoContext.Engine.Core.Rpc.Results;
using AutoContext.Engine.Core.Tests.Support.Infrastructure.Events;
using AutoContext.Engine.Core.Tests.Support.Rpc;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages.Agent;
using AutoContext.Engine.Protocol.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class AgentRpcHandlerTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_broadcaster()
        => Assert.Throws<ArgumentNullException>(() => new AgentRpcHandler(
            broadcaster: null!,
            logger: NullLogger<AgentRpcHandler>.Instance));

    [Fact]
    public void Should_throw_when_constructed_with_null_logger()
        => Assert.Throws<ArgumentNullException>(() => new AgentRpcHandler(
            CreateBroadcaster(),
            logger: null!));

    [Fact]
    public void Should_serve_the_five_notifications_and_the_subscribe_method()
    {
        var handler = new AgentRpcHandler(CreateBroadcaster(), NullLogger<AgentRpcHandler>.Instance);

        Assert.Equal(
            [
                AgentMethods.SubagentStarted,
                AgentMethods.SubagentStopped,
                AgentMethods.Compacted,
                AgentMethods.ToolUsed,
                AgentMethods.TurnEnded,
                AgentMethods.EventsSubscribe,
            ],
            handler.Methods);
    }

    [Fact]
    public async Task Should_rebroadcast_each_notification_family_as_a_mapped_event()
    {
        var broadcaster = CreateBroadcaster();
        var handler = new AgentRpcHandler(broadcaster, NullLogger<AgentRpcHandler>.Instance);
        var subscription = broadcaster.Subscribe();

        var ct = TestContext.Current.CancellationToken;
        var started = await handler.InvokeAsync(
            JsonRpcRequestTestFactory.BuildRequest(
                AgentMethods.SubagentStarted,
                new JsonAgentSubagentStartedParams { SessionId = "s-1", TaskPrompt = "port to c#" },
                ProtocolJsonContext.Default.JsonAgentSubagentStartedParams),
            ct);
        await handler.InvokeAsync(
            JsonRpcRequestTestFactory.BuildRequest(
                AgentMethods.SubagentStopped,
                new JsonAgentSubagentStoppedParams { SessionId = "s-1" },
                ProtocolJsonContext.Default.JsonAgentSubagentStoppedParams),
            ct);
        await handler.InvokeAsync(
            JsonRpcRequestTestFactory.BuildRequest(
                AgentMethods.Compacted,
                new JsonAgentCompactedParams { SessionId = "s-2" },
                ProtocolJsonContext.Default.JsonAgentCompactedParams),
            ct);
        await handler.InvokeAsync(
            JsonRpcRequestTestFactory.BuildRequest(
                AgentMethods.ToolUsed,
                new JsonAgentToolUsedParams
                {
                    SessionId = "s-2",
                    ToolName = "analyze_csharp_code_style",
                    Outcome = "success",
                },
                ProtocolJsonContext.Default.JsonAgentToolUsedParams),
            ct);
        await handler.InvokeAsync(
            JsonRpcRequestTestFactory.BuildRequest(
                AgentMethods.TurnEnded,
                new JsonAgentTurnEndedParams { SessionId = "s-2" },
                ProtocolJsonContext.Default.JsonAgentTurnEndedParams),
            ct);

        broadcaster.Complete();
        var events = await BroadcasterSubscriptionTestDrainer.DrainAsync(subscription);

        Assert.Multiple(
            () => Assert.IsType<NotificationHandlerResult>(started),
            () => Assert.Equal(5, events.Count),
            () => Assert.Equal(
                new JsonAgentEvent
                {
                    Kind = AgentEventKinds.SubagentStarted,
                    SessionId = "s-1",
                    TaskPrompt = "port to c#",
                },
                events[0]),
            () => Assert.Equal(
                new JsonAgentEvent { Kind = AgentEventKinds.SubagentStopped, SessionId = "s-1" },
                events[1]),
            () => Assert.Equal(
                new JsonAgentEvent { Kind = AgentEventKinds.Compacted, SessionId = "s-2" },
                events[2]),
            () => Assert.Equal(
                new JsonAgentEvent
                {
                    Kind = AgentEventKinds.ToolUsed,
                    SessionId = "s-2",
                    ToolName = "analyze_csharp_code_style",
                    Outcome = "success",
                },
                events[3]),
            () => Assert.Equal(
                new JsonAgentEvent { Kind = AgentEventKinds.TurnEnded, SessionId = "s-2" },
                events[4]));
    }

    [Fact]
    public async Task Should_drop_a_malformed_notification_without_publishing_or_replying()
    {
        var broadcaster = CreateBroadcaster();
        var handler = new AgentRpcHandler(broadcaster, NullLogger<AgentRpcHandler>.Instance);
        var subscription = broadcaster.Subscribe();
        var ct = TestContext.Current.CancellationToken;

        // Params is a JSON string, not the params object shape. A
        // notification carries no id, so the handler must not reply with
        // an error; it drops the event and keeps serving.
        var malformed = await handler.InvokeAsync(
            new JsonRpcRequest
            {
                Method = AgentMethods.SubagentStarted,
                Params = JsonSerializer.SerializeToElement("not-an-object"),
            },
            ct);

        // A valid notification follows so the drain has a deterministic
        // stopping point that proves the malformed one produced nothing.
        await handler.InvokeAsync(
            JsonRpcRequestTestFactory.BuildRequest(
                AgentMethods.TurnEnded,
                new JsonAgentTurnEndedParams { SessionId = "s-only" },
                ProtocolJsonContext.Default.JsonAgentTurnEndedParams),
            ct);

        broadcaster.Complete();
        var events = await BroadcasterSubscriptionTestDrainer.DrainAsync(subscription);

        Assert.Multiple(
            () => Assert.IsType<NotificationHandlerResult>(malformed),
            () => Assert.Equal(
                new JsonAgentEvent { Kind = AgentEventKinds.TurnEnded, SessionId = "s-only" },
                Assert.Single(events)));
    }

    [Fact]
    public async Task Should_fan_a_notification_out_to_every_subscriber()
    {
        var broadcaster = CreateBroadcaster();
        var handler = new AgentRpcHandler(broadcaster, NullLogger<AgentRpcHandler>.Instance);
        var first = broadcaster.Subscribe();
        var second = broadcaster.Subscribe();

        await handler.InvokeAsync(
            JsonRpcRequestTestFactory.BuildRequest(
                AgentMethods.Compacted,
                new JsonAgentCompactedParams { SessionId = "shared" },
                ProtocolJsonContext.Default.JsonAgentCompactedParams),
            TestContext.Current.CancellationToken);

        broadcaster.Complete();
        var firstEvents = await BroadcasterSubscriptionTestDrainer.DrainAsync(first);
        var secondEvents = await BroadcasterSubscriptionTestDrainer.DrainAsync(second);

        var expected = new JsonAgentEvent { Kind = AgentEventKinds.Compacted, SessionId = "shared" };
        Assert.Multiple(
            () => Assert.Equal(expected, Assert.Single(firstEvents)),
            () => Assert.Equal(expected, Assert.Single(secondEvents)));
    }

    [Fact]
    public async Task Should_stream_published_events_on_the_subscribe_method()
    {
        var broadcaster = CreateBroadcaster();
        var handler = new AgentRpcHandler(broadcaster, NullLogger<AgentRpcHandler>.Instance);

        var result = Assert.IsType<StreamingHandlerResult>(
            await handler.InvokeAsync(
                JsonRpcRequestTestFactory.BuildRequest(AgentMethods.EventsSubscribe),
                TestContext.Current.CancellationToken));

        // Publish after the subscription is enrolled (pure live tail),
        // then complete so the stream reaches a clean EOF.
        broadcaster.TryPublish(new JsonAgentEvent { Kind = AgentEventKinds.TurnEnded, SessionId = "a" });
        broadcaster.TryPublish(new JsonAgentEvent { Kind = AgentEventKinds.Compacted, SessionId = "b" });
        broadcaster.Complete();

        var events = await ReadPayloadsAsync(result);

        Assert.Multiple(
            () => Assert.Equal(
                new JsonAgentEvent { Kind = AgentEventKinds.TurnEnded, SessionId = "a" }, events[0]),
            () => Assert.Equal(
                new JsonAgentEvent { Kind = AgentEventKinds.Compacted, SessionId = "b" }, events[1]),
            () => Assert.Equal(2, events.Count));
    }

    private static Broadcaster<JsonAgentEvent> CreateBroadcaster()
        => new(NullLogger.Instance, "agent-events");

    private static async Task<List<JsonAgentEvent>> ReadPayloadsAsync(StreamingHandlerResult result)
    {
        var events = new List<JsonAgentEvent>();
        await foreach (var element in result.Payloads
            .WithCancellation(TestContext.Current.CancellationToken)
            .ConfigureAwait(false))
        {
            events.Add(element.Deserialize(ProtocolJsonContext.Default.JsonAgentEvent)!);
        }

        return events;
    }
}
