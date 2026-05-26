namespace AutoContext.Engine.Core.Tests.Support.Logging;

using System.Threading.Channels;

using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Protocol.Messages.Logs;

internal static class LogSubscriberTestFactory
{
    public static LogSubscriber Create()
        => new(Channel.CreateUnbounded<LogRecord>());
}
