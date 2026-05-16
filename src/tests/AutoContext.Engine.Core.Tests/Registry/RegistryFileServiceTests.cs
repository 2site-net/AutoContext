namespace AutoContext.Engine.Core.Tests.Registry;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Core.Tests.Testing.Utils;

/// <summary>
/// Tests for <see cref="RegistryFileService"/> — the hosted
/// coordinator that owns the dedicated worker thread, the
/// cross-process named mutex, and the read-modify-write cycle on
/// top of <see cref="RegistryFileReader"/> and
/// <see cref="RegistryFileWriter"/>.
/// </summary>
public sealed class RegistryFileServiceTests : IDisposable
{
    private readonly string _directory;
    private readonly string _path;

    public RegistryFileServiceTests()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            $"ac-registry-service-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "engine-registry.json");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }

    [Fact]
    public void Constructor_should_reject_null_or_whitespace_path()
    {
        Assert.Multiple(
            () => Assert.Throws<ArgumentNullException>(() => new RegistryFileService(null!)),
            () => Assert.Throws<ArgumentException>(() => new RegistryFileService(string.Empty)),
            () => Assert.Throws<ArgumentException>(() => new RegistryFileService("   ")));
    }

    [Fact]
    public async Task WriteAsync_should_persist_a_single_request_through_the_worker()
    {
        await using var sut = CreateService();
        await sut.StartAsync(TestContext.Current.CancellationToken);
        var entry = RegistryEntryFakeData.CreateValidEntry();

        await sut.WriteAsync(_ => [entry], TestContext.Current.CancellationToken);

        var reader = new RegistryFileReader(_path);
        var persisted = await reader.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Single(persisted);
        Assert.Equal(entry.InstanceId, persisted[0].InstanceId);
    }

    [Fact]
    public async Task WriteAsync_should_serialise_concurrent_in_process_appends_without_lost_updates()
    {
        await using var sut = CreateService();
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

        var reader = new RegistryFileReader(_path);
        var persisted = await reader.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(concurrentAppends, persisted.Count);
    }

    [Fact]
    public async Task WriteAsync_should_recover_from_corrupt_file_by_starting_from_empty()
    {
        await File.WriteAllTextAsync(_path, "not json at all", TestContext.Current.CancellationToken);

        await using var sut = CreateService();
        await sut.StartAsync(TestContext.Current.CancellationToken);
        var entry = RegistryEntryFakeData.CreateValidEntry();
        IReadOnlyList<RegistryEntry>? observedCurrent = null;

        await sut.WriteAsync(
            current =>
            {
                observedCurrent = current;
                return [entry];
            },
            TestContext.Current.CancellationToken);

        Assert.NotNull(observedCurrent);
        Assert.Empty(observedCurrent!);

        var reader = new RegistryFileReader(_path);
        var persisted = await reader.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Single(persisted);
        Assert.Equal(entry.InstanceId, persisted[0].InstanceId);
    }

    [Fact]
    public async Task WriteAsync_should_fault_when_transform_throws()
    {
        await using var sut = CreateService();
        await sut.StartAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.WriteAsync(
                _ => throw new InvalidOperationException("boom"),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WriteAsync_should_throw_TimeoutException_when_peer_holds_cross_process_mutex()
    {
        var mutexName = RegistryFileService.ComposeMutexName(_path);

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
            await using var sut = CreateService(new RegistryFileServiceOptions
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
    public async Task StopAsync_should_drain_an_already_queued_request()
    {
        await using var sut = CreateService();
        await sut.StartAsync(TestContext.Current.CancellationToken);
        var entry = RegistryEntryFakeData.CreateValidEntry();
        var write = sut.WriteAsync(_ => [entry], TestContext.Current.CancellationToken);

        await sut.StopAsync(TestContext.Current.CancellationToken);
        await write;

        var reader = new RegistryFileReader(_path);
        var persisted = await reader.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Single(persisted);
    }

    [Fact]
    public async Task WriteAsync_should_reject_requests_after_StopAsync()
    {
        var sut = CreateService();
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await sut.StopAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.WriteAsync(_ => [], TestContext.Current.CancellationToken));

        await sut.DisposeAsync();
    }

    [Fact]
    public async Task WriteAsync_should_fault_when_transform_returns_null()
    {
        await using var sut = CreateService();
        await sut.StartAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.WriteAsync(_ => null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StopAsync_should_cancel_pre_start_writes_so_callers_do_not_hang()
    {
        // Submit a request *before* StartAsync. There is no worker
        // thread to drain the channel; StopAsync must finalise the
        // request itself so the caller's await does not hang.
        var sut = CreateService();
        var write = sut.WriteAsync(
            _ => [RegistryEntryFakeData.CreateValidEntry()],
            TestContext.Current.CancellationToken);

        await sut.StopAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<TaskCanceledException>(() => write);
        await sut.DisposeAsync();
    }

    private RegistryFileService CreateService(
        RegistryFileServiceOptions? options = null) =>
        new(
            _path,
            options,
            new RegistryFileReaderOptions
            {
                InitialRetryDelay = TimeSpan.FromMilliseconds(1),
                MaxRetryDelay = TimeSpan.FromMilliseconds(5),
                MaxAttempts = 5,
            });
}
