namespace AutoContext.Engine.Core.Tests.Support.Logging.Primitives;

using AutoContext.Engine.Core.Logging.Primitives;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

internal static class RetentionPolicyTestFactory
{
    public static RetentionPolicy Create(TimeSpan window, DateTimeOffset now) =>
        new(
            Options.Create(new EngineOptions { Retention = window }),
            new FakeTimeProvider(now));
}
