namespace AutoContext.Engine.Core.Tests.Support.Workspace.Config;

using AutoContext.Engine.Core.Workspace.Config;

using Microsoft.Extensions.Logging.Abstractions;

internal static class ConfigSubscriptionBroadcasterTestFactory
{
    public static ConfigSubscriptionBroadcaster Create()
        => new(NullLogger<ConfigSubscriptionBroadcaster>.Instance);
}
