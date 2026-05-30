namespace AutoContext.Engine.Core.Tests.Registry;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Core.Tests.Support.Registry;
using AutoContext.Engine.Core.Tests.Support.Shared;
using AutoContext.Engine.Protocol.Messages.Registry;

public sealed class RegistryFileServiceTests
{
    private const string RegistryFileName = "engine-registry.json";

    public sealed class Constructor
    {
        [Fact]
        public void Should_reject_null_or_whitespace_path()
        {
            Assert.Multiple(
                () => Assert.Throws<ArgumentNullException>(() => new RegistryFileService(null!)),
                () => Assert.Throws<ArgumentException>(() => new RegistryFileService(string.Empty)),
                () => Assert.Throws<ArgumentException>(() => new RegistryFileService("   ")));
        }
    }

    public sealed class WriteAsync(TempDirectoryFixture tempDirectory) : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public async Task Should_persist_a_single_request_through_the_worker()
        {
            var path = tempDirectory.CreatePath(RegistryFileName);
            await using var sut = RegistryFileServiceTestFactory.CreateService(path);
            await sut.StartAsync(TestContext.Current.CancellationToken);
            var entry = RegistryEntryFakeData.CreateValidEntry();

            await sut.WriteAsync(_ => [entry], TestContext.Current.CancellationToken);

            var reader = new RegistryFileReader(path);
            var persisted = await reader.ReadAsync(TestContext.Current.CancellationToken);
            Assert.Single(persisted);
            Assert.Equal(entry.InstanceId, persisted[0].InstanceId);
        }

        [Fact]
        public async Task Should_serialise_concurrent_in_process_appends_without_lost_updates()
        {
            var path = tempDirectory.CreatePath(RegistryFileName);
            await using var sut = RegistryFileServiceTestFactory.CreateService(path);
            await sut.StartAsync(TestContext.Current.CancellationToken);

            const int concurrentAppends = 16;
            var appendTasks = Enumerable.Range(0, concurrentAppends)
                .Select(_ =>
                {
                    var entry = RegistryEntryFakeData.CreateValidEntry();
                    return sut.WriteAsync(
                        current => [.. current, entry],
                        TestContext.Current.CancellationToken);
                })
                .ToArray();

            await Task.WhenAll(appendTasks);

            var reader = new RegistryFileReader(path);
            var persisted = await reader.ReadAsync(TestContext.Current.CancellationToken);
            Assert.Equal(concurrentAppends, persisted.Count);
        }

        [Fact]
        public async Task Should_recover_from_corrupt_file_by_starting_from_empty()
        {
            var path = tempDirectory.CreatePath(RegistryFileName);
            await File.WriteAllTextAsync(path, "not json at all", TestContext.Current.CancellationToken);

            await using var sut = RegistryFileServiceTestFactory.CreateService(path);
            await sut.StartAsync(TestContext.Current.CancellationToken);
            var entry = RegistryEntryFakeData.CreateValidEntry();
            IReadOnlyList<JsonRegistryEntry>? observedCurrent = null;

            await sut.WriteAsync(
                current =>
                {
                    observedCurrent = current;
                    return [entry];
                },
                TestContext.Current.CancellationToken);

            Assert.NotNull(observedCurrent);
            Assert.Empty(observedCurrent!);

            var reader = new RegistryFileReader(path);
            var persisted = await reader.ReadAsync(TestContext.Current.CancellationToken);
            Assert.Single(persisted);
            Assert.Equal(entry.InstanceId, persisted[0].InstanceId);
        }

        [Fact]
        public async Task Should_fault_when_transform_throws()
        {
            var path = tempDirectory.CreatePath(RegistryFileName);
            await using var sut = RegistryFileServiceTestFactory.CreateService(path);
            await sut.StartAsync(TestContext.Current.CancellationToken);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.WriteAsync(
                    _ => throw new InvalidOperationException("boom"),
                    TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_throw_TimeoutException_when_peer_holds_cross_process_mutex()
        {
            var path = tempDirectory.CreatePath(RegistryFileName);
            var mutexName = RegistryFileService.ComposeMutexName(path);

            using var peerHolds = new ManualResetEventSlim(false);
            using var releasePeer = new ManualResetEventSlim(false);
            var peerThread = new Thread(() =>
            {
                using var peerMutex = new Mutex(initiallyOwned: false, mutexName);
                peerMutex.WaitOne();
                try
                {
                    peerHolds.Set();
                    releasePeer.Wait();
                }
                finally
                {
                    peerMutex.ReleaseMutex();
                }
            })
            {
                IsBackground = true,
                Name = "ac-registry-service-tests-peer",
            };
            peerThread.Start();
            peerHolds.Wait(TestContext.Current.CancellationToken);

            try
            {
                await using var sut = RegistryFileServiceTestFactory.CreateService(
                    path,
                    new RegistryFileServiceOptions
                    {
                        MutexAcquireTimeout = TimeSpan.FromMilliseconds(50),
                    });
                await sut.StartAsync(TestContext.Current.CancellationToken);

                await Assert.ThrowsAsync<TimeoutException>(
                    () => sut.WriteAsync(
                        _ => [RegistryEntryFakeData.CreateValidEntry()],
                        TestContext.Current.CancellationToken));
            }
            finally
            {
                releasePeer.Set();
                peerThread.Join();
            }
        }

        [Fact]
        public async Task Should_reject_requests_after_StopAsync()
        {
            var path = tempDirectory.CreatePath(RegistryFileName);
            var sut = RegistryFileServiceTestFactory.CreateService(path);
            await sut.StartAsync(TestContext.Current.CancellationToken);
            await sut.StopAsync(TestContext.Current.CancellationToken);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.WriteAsync(_ => [], TestContext.Current.CancellationToken));

            await sut.DisposeAsync();
        }

        [Fact]
        public async Task Should_fault_when_transform_returns_null()
        {
            var path = tempDirectory.CreatePath(RegistryFileName);
            await using var sut = RegistryFileServiceTestFactory.CreateService(path);
            await sut.StartAsync(TestContext.Current.CancellationToken);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.WriteAsync(_ => null!, TestContext.Current.CancellationToken));
        }
    }

    public sealed class StartAsync(TempDirectoryFixture tempDirectory) : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public async Task Should_append_own_entry_when_factory_is_supplied()
        {
            var path = tempDirectory.CreatePath(RegistryFileName);
            var ownEntry = RegistryEntryFakeData.CreateValidEntry();
            await using var sut = RegistryFileServiceTestFactory.CreateService(path, ownEntryFactory: () => ownEntry);

            await sut.StartAsync(TestContext.Current.CancellationToken);

            var reader = new RegistryFileReader(path);
            var persisted = await reader.ReadAsync(TestContext.Current.CancellationToken);
            Assert.Single(persisted);
            Assert.Equal(ownEntry.InstanceId, persisted[0].InstanceId);
        }
    }

    public sealed class StopAsync(TempDirectoryFixture tempDirectory) : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public async Task Should_drain_an_already_queued_request()
        {
            var path = tempDirectory.CreatePath(RegistryFileName);
            await using var sut = RegistryFileServiceTestFactory.CreateService(path);
            await sut.StartAsync(TestContext.Current.CancellationToken);
            var entry = RegistryEntryFakeData.CreateValidEntry();
            var write = sut.WriteAsync(_ => [entry], TestContext.Current.CancellationToken);

            await sut.StopAsync(TestContext.Current.CancellationToken);
            await write;

            var reader = new RegistryFileReader(path);
            var persisted = await reader.ReadAsync(TestContext.Current.CancellationToken);
            Assert.Single(persisted);
        }

        [Fact]
        public async Task Should_cancel_pre_start_writes_so_callers_do_not_hang()
        {
            var path = tempDirectory.CreatePath(RegistryFileName);
            var sut = RegistryFileServiceTestFactory.CreateService(path);
            var write = sut.WriteAsync(
                _ => [RegistryEntryFakeData.CreateValidEntry()],
                TestContext.Current.CancellationToken);

            await sut.StopAsync(TestContext.Current.CancellationToken);

            await Assert.ThrowsAsync<TaskCanceledException>(() => write);
            await sut.DisposeAsync();
        }

        [Fact]
        public async Task Should_remove_only_own_entry_leaving_peer_rows_intact()
        {
            var path = tempDirectory.CreatePath(RegistryFileName);
            var ownEntry = RegistryEntryFakeData.CreateValidEntry();
            var peerEntry = RegistryEntryFakeData.CreateValidEntry();
            await using var sut = RegistryFileServiceTestFactory.CreateService(path, ownEntryFactory: () => ownEntry);

            await sut.StartAsync(TestContext.Current.CancellationToken);
            await sut.WriteAsync(
                current => [.. current, peerEntry],
                TestContext.Current.CancellationToken);

            await sut.StopAsync(TestContext.Current.CancellationToken);

            var reader = new RegistryFileReader(path);
            var persisted = await reader.ReadAsync(TestContext.Current.CancellationToken);
            Assert.Multiple(
                () => Assert.Single(persisted),
                () => Assert.Equal(peerEntry.InstanceId, persisted[0].InstanceId));
        }

        [Fact]
        public async Task Should_swallow_own_entry_removal_cancellation()
        {
            var path = tempDirectory.CreatePath(RegistryFileName);
            var ownEntry = RegistryEntryFakeData.CreateValidEntry();
            await using var sut = RegistryFileServiceTestFactory.CreateService(path, ownEntryFactory: () => ownEntry);
            await sut.StartAsync(TestContext.Current.CancellationToken);

            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            await sut.StopAsync(cts.Token);

            var reader = new RegistryFileReader(path);
            var persisted = await reader.ReadAsync(TestContext.Current.CancellationToken);
            Assert.Single(persisted, e => e.InstanceId == ownEntry.InstanceId);
        }

        [Fact]
        public async Task Should_be_idempotent_for_own_entry_removal()
        {
            var path = tempDirectory.CreatePath(RegistryFileName);
            var ownEntry = RegistryEntryFakeData.CreateValidEntry();
            var sut = RegistryFileServiceTestFactory.CreateService(path, ownEntryFactory: () => ownEntry);

            await sut.StartAsync(TestContext.Current.CancellationToken);
            await sut.StopAsync(TestContext.Current.CancellationToken);
            await sut.StopAsync(TestContext.Current.CancellationToken);

            await sut.DisposeAsync();
        }
    }
}
