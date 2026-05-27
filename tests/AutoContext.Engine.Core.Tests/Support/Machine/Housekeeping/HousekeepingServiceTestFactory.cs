namespace AutoContext.Engine.Core.Tests.Support.Machine.Housekeeping;

using AutoContext.Engine.Core.Machine;
using AutoContext.Engine.Core.Machine.Housekeeping;
using AutoContext.Engine.Core.Tests.Support.Watchdogs;

using Microsoft.Extensions.Logging.Abstractions;

internal static class HousekeepingServiceTestFactory
{
    public static HousekeepingService Create(string cacheRoot, TimeSpan engineRetention)
        => Create(
            cacheRoot,
            engineRetention,
            Path.Combine(cacheRoot, EngineCacheLayout.RegistryFileName),
            new FakeProcessLookup());

    public static HousekeepingService Create(
        string cacheRoot,
        TimeSpan engineRetention,
        string registryPath,
        FakeProcessLookup lookup)
    {
        var scanner = CacheRootScannerTestFactory.Create(cacheRoot, registryPath, lookup);
        var cleaner = StaleSubtreeCleanerTestFactory.Create(engineRetention, DateTimeOffset.UtcNow);
        return new HousekeepingService(
            scanner,
            cleaner,
            TimeProvider.System,
            NullLogger<HousekeepingService>.Instance);
    }
}
