namespace AutoContext.Engine.Core.Tests.Machine.Housekeeping;

using AutoContext.Engine.Core.Machine.Housekeeping;
using AutoContext.Engine.Core.Tests.Support.Logging;
using AutoContext.Engine.Core.Tests.Support.Machine.Housekeeping;
using AutoContext.Engine.Core.Tests.Support.Registry;
using AutoContext.Engine.Core.Tests.Support.Shared;
using AutoContext.Engine.Core.Tests.Support.Watchdogs;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class HousekeepingServiceTests
{
    public sealed class Constructor
    {
        [Fact]
        public void Should_reject_invalid_arguments()
        {
            // Arrange
            var scanner = CacheRootScannerTestFactory.Create(
                Path.GetTempPath(),
                Path.Combine(Path.GetTempPath(), "engine-registry.json"),
                new FakeProcessLookup());
            var cleaner = StaleSubtreeCleanerTestFactory.Create(TimeSpan.Zero, DateTimeOffset.UtcNow);

            // Act + Assert
            Assert.Multiple(
                () => Assert.Throws<ArgumentNullException>(
                    () => new HousekeepingService(null!, cleaner, TimeProvider.System, NullLogger<HousekeepingService>.Instance)),
                () => Assert.Throws<ArgumentNullException>(
                    () => new HousekeepingService(scanner, null!, TimeProvider.System, NullLogger<HousekeepingService>.Instance)),
                () => Assert.Throws<ArgumentNullException>(
                    () => new HousekeepingService(scanner, cleaner, null!, NullLogger<HousekeepingService>.Instance)),
                () => Assert.Throws<ArgumentNullException>(
                    () => new HousekeepingService(scanner, cleaner, TimeProvider.System, null!)));
        }
    }

    public sealed class StartAsync(TempDirectoryFixture tempDirectory) : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public async Task Should_be_a_no_op()
        {
            // Arrange — cache root holds a Foreign subtree the
            // sweep would normally reap. StartAsync must not
            // touch it.
            var cacheRoot = tempDirectory.CreateDirectory();
            var foreign = Path.Combine(cacheRoot, "not-a-workspace-hash");
            Directory.CreateDirectory(foreign);
            var sut = HousekeepingServiceTestFactory.Create(cacheRoot, TimeSpan.Zero);

            // Act
            await sut.StartAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(Directory.Exists(foreign));
        }
    }

    public sealed class StopAsync(TempDirectoryFixture tempDirectory) : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public async Task Should_reap_expired_subtrees_under_engine_retention()
        {
            // Arrange — engine --retention is Zero so Foreign and
            // Unregistered subtrees expire immediately. No registry,
            // so a canonical <wsHash>/<guid> child classifies as
            // Unregistered.
            var cacheRoot = tempDirectory.CreateDirectory();
            var foreign = Path.Combine(cacheRoot, "not-a-workspace-hash");
            Directory.CreateDirectory(foreign);
            var unregistered = CanonicalCacheLayoutTestSeeder.CreateInstanceSubtree(cacheRoot, Guid.NewGuid());
            var sut = HousekeepingServiceTestFactory.Create(cacheRoot, TimeSpan.Zero);

            // Act
            await sut.StopAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.False(Directory.Exists(foreign)),
                () => Assert.False(Directory.Exists(unregistered)));
        }

        [Fact]
        public async Task Should_preserve_Registered_subtree_with_live_pid()
        {
            // Arrange — registered entry whose pid resolves to the
            // start time embedded in the registry entry.
            var cacheRoot = tempDirectory.CreateDirectory();
            var instanceId = Guid.NewGuid();
            var startTime = DateTimeOffset.UtcNow.AddMinutes(-2);
            var liveSubtree = CanonicalCacheLayoutTestSeeder.CreateInstanceSubtree(cacheRoot, instanceId);

            var entry = RegistryEntryFakeData.CreateValidEntry() with
            {
                InstanceId = instanceId,
                ProcessId = 4242,
                ProcessStartTimeUtc = startTime,
            };
            var registryPath = RegistryFileTestWriter.WriteToCache(cacheRoot, entry);
            var lookup = new FakeProcessLookup();
            lookup.Register(4242, new FakeProcessHandle(startTime.UtcDateTime));

            var sut = HousekeepingServiceTestFactory.Create(cacheRoot, TimeSpan.Zero, registryPath, lookup);

            // Act
            await sut.StopAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(Directory.Exists(liveSubtree));
        }

        [Fact]
        public async Task Should_complete_when_cache_root_does_not_exist()
        {
            // Arrange — scanner returns an empty list; the service
            // must not throw and must not create the cache root.
            var nonexistentCacheRoot = Path.Combine(tempDirectory.CreateDirectory(), "missing");
            var sut = HousekeepingServiceTestFactory.Create(nonexistentCacheRoot, TimeSpan.Zero);

            // Act
            await sut.StopAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.False(Directory.Exists(nonexistentCacheRoot));
        }

        [Fact]
        public async Task Should_complete_when_outer_token_is_already_cancelled()
        {
            // Arrange — a host that's already past its shutdown
            // deadline must not cause this service to fault.
            var cacheRoot = tempDirectory.CreateDirectory();
            var foreign = Path.Combine(cacheRoot, "not-a-workspace-hash");
            Directory.CreateDirectory(foreign);
            var sut = HousekeepingServiceTestFactory.Create(cacheRoot, TimeSpan.Zero);
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // Act + Assert — must not throw.
            await sut.StopAsync(cts.Token);
        }
    }
}
