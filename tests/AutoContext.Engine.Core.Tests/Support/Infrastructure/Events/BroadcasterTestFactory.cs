namespace AutoContext.Engine.Core.Tests.Support.Infrastructure.Events;

using AutoContext.Engine.Core.Infrastructure.Events;

using Microsoft.Extensions.Logging.Abstractions;

internal static class BroadcasterTestFactory
{
    public static Broadcaster<BroadcasterTestPayload> Create(string channel = "test-channel")
        => new(NullLogger<Broadcaster<BroadcasterTestPayload>>.Instance, channel);

    public static Broadcaster<TPayload> Create<TPayload>(string channel)
        where TPayload : class
        => new(NullLogger<Broadcaster<TPayload>>.Instance, channel);
}
