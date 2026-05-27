namespace AutoContext.Engine.Core.Tests.Support.Machine.Housekeeping;

using AutoContext.Engine.Core.Machine.Housekeeping;
using AutoContext.Engine.Core.Tests.Support.Logging.Primitives;

using Microsoft.Extensions.Logging.Abstractions;

internal static class StaleSubtreeCleanerTestFactory
{
    public static StaleSubtreeCleaner Create(TimeSpan engineRetention, DateTimeOffset now)
    {
        var policy = RetentionPolicyTestFactory.Create(engineRetention, now);
        return new StaleSubtreeCleaner(policy, NullLogger<StaleSubtreeCleaner>.Instance);
    }
}
