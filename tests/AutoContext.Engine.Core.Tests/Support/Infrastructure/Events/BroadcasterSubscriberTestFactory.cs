namespace AutoContext.Engine.Core.Tests.Support.Infrastructure.Events;

using System.Threading.Channels;

using AutoContext.Engine.Core.Infrastructure.Events;

internal static class BroadcasterSubscriberTestFactory
{
    public static BroadcasterSubscriber<BroadcasterTestPayload> Create()
        => new(Channel.CreateUnbounded<BroadcasterTestPayload>());
}
