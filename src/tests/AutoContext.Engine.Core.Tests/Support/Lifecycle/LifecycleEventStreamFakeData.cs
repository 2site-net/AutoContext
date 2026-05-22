namespace AutoContext.Engine.Core.Tests.Support.Lifecycle;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Lifecycle;
using AutoContext.Engine.Protocol.Messages.Lifecycle;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

internal static class LifecycleEventStreamFakeData
{
    public static LifecycleEventStream CreateStream(EngineOptions options) =>
        new(Options.Create(options), NullLogger<LifecycleEventStream>.Instance);

    public static LifecycleEvent CreateTerminalEvent() =>
        new()
        {
            Kind = LifecycleEventKinds.ShuttingDown,
            InstanceId = Guid.NewGuid(),
            Revision = 0,
        };
}
