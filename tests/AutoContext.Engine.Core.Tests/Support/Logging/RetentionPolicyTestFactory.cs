namespace AutoContext.Engine.Core.Tests.Support.Logging;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Tests.Support.Shared;

using Microsoft.Extensions.Options;

internal static class RetentionPolicyTestFactory
{
    public static RetentionPolicy Create(TimeSpan window, DateTimeOffset now) =>
        new(
            Options.Create(new EngineOptions { Retention = window }),
            new FakeTimeProvider(now));
}
