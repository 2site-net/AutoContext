namespace AutoContext.Engine.Core.Tests.Support.Infrastructure.Events;

using System.Threading.Channels;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Protocol.Messages.Logs;

internal static class SubscriberTestFactory
{
    public static Subscriber<JsonLogRecord> Create()
        => new(Channel.CreateUnbounded<JsonLogRecord>());
}
