namespace AutoContext.Engine.Core.Tests.Support.Infrastructure.Events;

using AutoContext.Engine.Core.Infrastructure.Events;

using Microsoft.Extensions.Logging.Abstractions;

internal static class SnapshotBroadcasterTestFactory
{
    public static SnapshotBroadcaster<BroadcasterTestPayload> Create(string channel = "test-channel")
        => new(NullLogger<SnapshotBroadcaster<BroadcasterTestPayload>>.Instance, channel);

    public static SnapshotBroadcaster<TPayload> Create<TPayload>(string channel)
        where TPayload : class
        => new(NullLogger<SnapshotBroadcaster<TPayload>>.Instance, channel);
}
