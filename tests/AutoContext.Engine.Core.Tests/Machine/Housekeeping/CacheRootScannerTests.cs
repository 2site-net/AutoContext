namespace AutoContext.Engine.Core.Tests.Machine.Housekeeping;

using AutoContext.Engine.Core.Infrastructure.Storage;
using AutoContext.Engine.Core.Machine;
using AutoContext.Engine.Core.Machine.Housekeeping;
using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Core.Tests.Support;
using AutoContext.Engine.Core.Tests.Support.Machine.Housekeeping;
using AutoContext.Engine.Core.Tests.Support.Registry;
using AutoContext.Engine.Core.Tests.Support.Shared;
using AutoContext.Engine.Core.Tests.Support.Watchdogs;
using AutoContext.Engine.Protocol.Messages.Registry;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

public sealed class CacheRootScannerTests
{
    public sealed class Constructor
    {
        [Fact]
        public void Should_reject_invalid_arguments()
        {
            // Arrange
            var entryReader = RegistryEntryReaderTestFactory.Create("ignored.json", new FakeProcessLookup());
            var cacheRoot = new CacheRoot(Options.Create(new EngineOptions
            {
                WorkspacePath = EngineOptionsFakeData.GetWorkspacePath(),
                InstanceId = Guid.NewGuid(),
                CacheRootOverride = Path.GetTempPath(),
            }));

            // Act + Assert
            Assert.Multiple(
                () => Assert.Throws<ArgumentNullException>(
                    () => new CacheRootScanner(null!, entryReader, NullLogger<CacheRootScanner>.Instance)),
                () => Assert.Throws<ArgumentNullException>(
                    () => new CacheRootScanner(cacheRoot, null!, NullLogger<CacheRootScanner>.Instance)),
                () => Assert.Throws<ArgumentNullException>(
                    () => new CacheRootScanner(cacheRoot, entryReader, null!)));
        }
    }

    public sealed class ScanAsync(TempDirectoryFixture tempDirectory) : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public async Task Should_return_empty_when_cache_root_directory_is_missing()
        {
            // Arrange
            var cacheRoot = Path.Combine(tempDirectory.CreateDirectory(), "missing");
            var registryPath = Path.Combine(cacheRoot, EngineCacheLayout.RegistryFileName);
            var sut = CacheRootScannerTestFactory.Create(cacheRoot, registryPath, new FakeProcessLookup());

            // Act
            var results = await sut.ScanAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task Should_return_empty_when_cache_root_directory_is_empty()
        {
            // Arrange
            var cacheRoot = tempDirectory.CreateDirectory();
            var registryPath = Path.Combine(cacheRoot, EngineCacheLayout.RegistryFileName);
            var sut = CacheRootScannerTestFactory.Create(cacheRoot, registryPath, new FakeProcessLookup());

            // Act
            var results = await sut.ScanAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task Should_classify_canonical_subtree_with_live_entry_as_Registered()
        {
            // Arrange
            var cacheRoot = tempDirectory.CreateDirectory();
            var instanceId = Guid.NewGuid();
            var startTime = DateTimeOffset.UtcNow.AddMinutes(-2);
            var instanceSubtree = CanonicalCacheLayoutTestSeeder.CreateInstanceSubtree(cacheRoot, instanceId);
            var entry = RegistryEntryFakeData.CreateValidEntry() with
            {
                WorkspaceHash = RegistryEntryFakeData.CanonicalWorkspaceHash,
                InstanceId = instanceId,
                ProcessId = 4242,
                ProcessStartTimeUtc = startTime,
            };
            var registryPath = RegistryFileTestWriter.WriteToCache(cacheRoot, entry);
            var lookup = new FakeProcessLookup();
            lookup.Register(4242, new FakeProcessHandle(startTime.UtcDateTime));
            var sut = CacheRootScannerTestFactory.Create(cacheRoot, registryPath, lookup);

            // Act
            var results = await sut.ScanAsync(TestContext.Current.CancellationToken);

            // Assert
            var status = Assert.Single(results);
            var registered = Assert.IsType<SubtreeRegistryStatus.Registered>(status);
            Assert.Multiple(
                () => Assert.Equal(instanceSubtree, registered.SubtreePath),
                () => Assert.Equal(instanceId, registered.Entry.InstanceId));
        }

        [Fact]
        public async Task Should_classify_canonical_subtree_with_dead_pid_as_StaleRegistration()
        {
            // Arrange
            var cacheRoot = tempDirectory.CreateDirectory();
            var instanceId = Guid.NewGuid();
            var instanceSubtree = CanonicalCacheLayoutTestSeeder.CreateInstanceSubtree(cacheRoot, instanceId);
            var entry = RegistryEntryFakeData.CreateValidEntry() with
            {
                WorkspaceHash = RegistryEntryFakeData.CanonicalWorkspaceHash,
                InstanceId = instanceId,
                ProcessId = 5151,
                ProcessStartTimeUtc = DateTimeOffset.UtcNow.AddMinutes(-30),
            };
            var registryPath = RegistryFileTestWriter.WriteToCache(cacheRoot, entry);
            var sut = CacheRootScannerTestFactory.Create(cacheRoot, registryPath, new FakeProcessLookup());

            // Act
            var results = await sut.ScanAsync(TestContext.Current.CancellationToken);

            // Assert
            var status = Assert.Single(results);
            var stale = Assert.IsType<SubtreeRegistryStatus.StaleRegistration>(status);
            Assert.Multiple(
                () => Assert.Equal(instanceSubtree, stale.SubtreePath),
                () => Assert.Equal(instanceId, stale.Entry.InstanceId));
        }

        [Fact]
        public async Task Should_classify_canonical_subtree_with_no_registry_entry_as_Unregistered()
        {
            // Arrange
            var cacheRoot = tempDirectory.CreateDirectory();
            var instanceSubtree = CanonicalCacheLayoutTestSeeder.CreateInstanceSubtree(cacheRoot, Guid.NewGuid());
            var registryPath = Path.Combine(cacheRoot, EngineCacheLayout.RegistryFileName);
            var sut = CacheRootScannerTestFactory.Create(cacheRoot, registryPath, new FakeProcessLookup());

            // Act
            var results = await sut.ScanAsync(TestContext.Current.CancellationToken);

            // Assert
            var status = Assert.Single(results);
            var unregistered = Assert.IsType<SubtreeRegistryStatus.Unregistered>(status);
            Assert.Equal(instanceSubtree, unregistered.SubtreePath);
        }

        [Fact]
        public async Task Should_classify_legacy_flat_workspace_hash_instance_directory_as_Foreign()
        {
            // Arrange
            var cacheRoot = tempDirectory.CreateDirectory();
            var flat = Path.Combine(cacheRoot, $"{RegistryEntryFakeData.CanonicalWorkspaceHash}#{Guid.NewGuid():D}");
            Directory.CreateDirectory(flat);
            var registryPath = Path.Combine(cacheRoot, EngineCacheLayout.RegistryFileName);
            var sut = CacheRootScannerTestFactory.Create(cacheRoot, registryPath, new FakeProcessLookup());

            // Act
            var results = await sut.ScanAsync(TestContext.Current.CancellationToken);

            // Assert
            var status = Assert.Single(results);
            var foreign = Assert.IsType<SubtreeRegistryStatus.Foreign>(status);
            Assert.Equal(flat, foreign.SubtreePath);
        }

        [Fact]
        public async Task Should_classify_bare_workspace_hash_directory_with_no_instance_children_as_Foreign()
        {
            // Arrange
            var cacheRoot = tempDirectory.CreateDirectory();
            var bare = Path.Combine(cacheRoot, RegistryEntryFakeData.CanonicalWorkspaceHash);
            Directory.CreateDirectory(bare);
            var registryPath = Path.Combine(cacheRoot, EngineCacheLayout.RegistryFileName);
            var sut = CacheRootScannerTestFactory.Create(cacheRoot, registryPath, new FakeProcessLookup());

            // Act
            var results = await sut.ScanAsync(TestContext.Current.CancellationToken);

            // Assert
            var status = Assert.Single(results);
            var foreign = Assert.IsType<SubtreeRegistryStatus.Foreign>(status);
            Assert.Equal(bare, foreign.SubtreePath);
        }

        [Fact]
        public async Task Should_classify_non_hex_top_level_directory_as_Foreign()
        {
            // Arrange
            var cacheRoot = tempDirectory.CreateDirectory();
            var garbage = Path.Combine(cacheRoot, "not-a-workspace-hash");
            Directory.CreateDirectory(garbage);
            var registryPath = Path.Combine(cacheRoot, EngineCacheLayout.RegistryFileName);
            var sut = CacheRootScannerTestFactory.Create(cacheRoot, registryPath, new FakeProcessLookup());

            // Act
            var results = await sut.ScanAsync(TestContext.Current.CancellationToken);

            // Assert
            var status = Assert.Single(results);
            var foreign = Assert.IsType<SubtreeRegistryStatus.Foreign>(status);
            Assert.Equal(garbage, foreign.SubtreePath);
        }

        [Fact]
        public async Task Should_classify_non_guid_child_of_workspace_hash_directory_as_Foreign()
        {
            // Arrange
            var cacheRoot = tempDirectory.CreateDirectory();
            var workspaceDir = Path.Combine(cacheRoot, RegistryEntryFakeData.CanonicalWorkspaceHash);
            var garbageChild = Path.Combine(workspaceDir, "not-a-guid");
            Directory.CreateDirectory(garbageChild);
            var registryPath = Path.Combine(cacheRoot, EngineCacheLayout.RegistryFileName);
            var sut = CacheRootScannerTestFactory.Create(cacheRoot, registryPath, new FakeProcessLookup());

            // Act
            var results = await sut.ScanAsync(TestContext.Current.CancellationToken);

            // Assert
            var status = Assert.Single(results);
            var foreign = Assert.IsType<SubtreeRegistryStatus.Foreign>(status);
            Assert.Equal(garbageChild, foreign.SubtreePath);
        }

        [Fact]
        public async Task Should_classify_each_subtree_independently_across_arms()
        {
            // Arrange
            var cacheRoot = tempDirectory.CreateDirectory();

            var liveInstanceId = Guid.NewGuid();
            var liveStart = DateTimeOffset.UtcNow.AddMinutes(-1);
            var liveSubtree = CanonicalCacheLayoutTestSeeder.CreateInstanceSubtree(cacheRoot, liveInstanceId);

            var staleInstanceId = Guid.NewGuid();
            var staleSubtree = CanonicalCacheLayoutTestSeeder.CreateInstanceSubtree(cacheRoot, staleInstanceId);

            var unregisteredSubtree = CanonicalCacheLayoutTestSeeder.CreateInstanceSubtree(cacheRoot, Guid.NewGuid());

            var foreignFlat = Path.Combine(cacheRoot, $"{RegistryEntryFakeData.CanonicalWorkspaceHash}#{Guid.NewGuid():D}");
            Directory.CreateDirectory(foreignFlat);

            var liveEntry = RegistryEntryFakeData.CreateValidEntry() with
            {
                WorkspaceHash = RegistryEntryFakeData.CanonicalWorkspaceHash,
                InstanceId = liveInstanceId,
                ProcessId = 1001,
                ProcessStartTimeUtc = liveStart,
            };
            var staleEntry = RegistryEntryFakeData.CreateValidEntry() with
            {
                WorkspaceHash = RegistryEntryFakeData.CanonicalWorkspaceHash,
                InstanceId = staleInstanceId,
                ProcessId = 1002,
                ProcessStartTimeUtc = DateTimeOffset.UtcNow.AddMinutes(-45),
            };
            var registryPath = RegistryFileTestWriter.WriteToCache(cacheRoot, liveEntry, staleEntry);

            var lookup = new FakeProcessLookup();
            lookup.Register(1001, new FakeProcessHandle(liveStart.UtcDateTime));

            var sut = CacheRootScannerTestFactory.Create(cacheRoot, registryPath, lookup);

            // Act
            var results = await sut.ScanAsync(TestContext.Current.CancellationToken);

            // Assert
            var byPath = results.ToDictionary(r => r.SubtreePath);
            Assert.Multiple(
                () => Assert.Equal(4, results.Count),
                () => Assert.IsType<SubtreeRegistryStatus.Registered>(byPath[liveSubtree]),
                () => Assert.IsType<SubtreeRegistryStatus.StaleRegistration>(byPath[staleSubtree]),
                () => Assert.IsType<SubtreeRegistryStatus.Unregistered>(byPath[unregisteredSubtree]),
                () => Assert.IsType<SubtreeRegistryStatus.Foreign>(byPath[foreignFlat]));
        }

        [Fact]
        public async Task Should_ignore_files_at_the_cache_root()
        {
            // Arrange
            var cacheRoot = tempDirectory.CreateDirectory();
            var strayFile = Path.Combine(cacheRoot, "stray.txt");
            await File.WriteAllTextAsync(strayFile, "ignored", TestContext.Current.CancellationToken);
            var registryPath = Path.Combine(cacheRoot, EngineCacheLayout.RegistryFileName);
            var sut = CacheRootScannerTestFactory.Create(cacheRoot, registryPath, new FakeProcessLookup());

            // Act
            var results = await sut.ScanAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task Should_propagate_cancellation()
        {
            // Arrange
            var cacheRoot = tempDirectory.CreateDirectory();
            CanonicalCacheLayoutTestSeeder.CreateInstanceSubtree(cacheRoot, Guid.NewGuid());
            var registryPath = Path.Combine(cacheRoot, EngineCacheLayout.RegistryFileName);
            var sut = CacheRootScannerTestFactory.Create(cacheRoot, registryPath, new FakeProcessLookup());
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // Act + Assert
            await Assert.ThrowsAsync<OperationCanceledException>(
                () => sut.ScanAsync(cts.Token));
        }

    }
}
