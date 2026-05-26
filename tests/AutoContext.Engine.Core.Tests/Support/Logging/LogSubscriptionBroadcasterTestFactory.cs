namespace AutoContext.Engine.Core.Tests.Support.Logging;

using AutoContext.Engine.Core.Logging;

using Microsoft.Extensions.Logging.Abstractions;

internal static class LogSubscriptionBroadcasterTestFactory
{
    public static LogSubscriptionBroadcaster Create()
        => new(NullLogger<LogSubscriptionBroadcaster>.Instance);
}
