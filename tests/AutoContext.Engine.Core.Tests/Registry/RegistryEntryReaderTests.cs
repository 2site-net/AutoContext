namespace AutoContext.Engine.Core.Tests.Registry;

using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Core.Tests.Support.Registry;
using AutoContext.Engine.Core.Tests.Support.Watchdogs;
using AutoContext.Engine.Protocol.Messages.Registry;
using AutoContext.Engine.Tests.Support.IO;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class RegistryEntryReaderTests
{
    private const string RegistryFileName = "engine-registry.json";

    public sealed class Constructor
    {
        [Fact]
        public void Should_reject_null_arguments()
        {
            // Arrange
            var fileReader = RegistryFileReaderTestFactory.Create("ignored.json");
            var lookup = new FakeProcessLookup();

            // Act + Assert
            Assert.Multiple(
                () => Assert.Throws<ArgumentNullException>(
                    () => new RegistryEntryReader(null!, lookup, NullLogger<RegistryEntryReader>.Instance)),
                () => Assert.Throws<ArgumentNullException>(
                    () => new RegistryEntryReader(fileReader, null!, NullLogger<RegistryEntryReader>.Instance)),
                () => Assert.Throws<ArgumentNullException>(
                    () => new RegistryEntryReader(fileReader, lookup, null!)));
        }
    }

    public sealed class ReadAsync(TempDirectoryFixture tempDirectory) : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public async Task Should_return_empty_when_registry_file_is_missing()
        {
            // Arrange
            var path = tempDirectory.CreatePath(RegistryFileName);
            var sut = RegistryEntryReaderTestFactory.Create(path, new FakeProcessLookup());

            // Act
            var results = await sut.ReadAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public async Task Should_mark_entry_Live_when_pid_alive_and_start_time_matches()
        {
            // Arrange
            var path = tempDirectory.CreatePath(RegistryFileName);
            var startTime = DateTimeOffset.UtcNow.AddMinutes(-3);
            var entry = RegistryEntryFakeData.CreateValidEntry() with
            {
                ProcessId = 4242,
                ProcessStartTimeUtc = startTime,
            };
            new RegistryFileWriter(path).Write([entry]);
            var lookup = new FakeProcessLookup();
            lookup.Register(4242, new FakeProcessHandle(startTime.UtcDateTime));
            var sut = RegistryEntryReaderTestFactory.Create(path, lookup);

            // Act
            var results = await sut.ReadAsync(TestContext.Current.CancellationToken);

            // Assert
            var result = Assert.Single(results);
            Assert.Multiple(
                () => Assert.Equal(entry.InstanceId, result.Entry.InstanceId),
                () => Assert.Equal(RegistryEntryProbeState.Live, result.State));
        }

        [Fact]
        public async Task Should_mark_entry_Stale_when_process_lookup_returns_null()
        {
            // Arrange
            var path = tempDirectory.CreatePath(RegistryFileName);
            var entry = RegistryEntryFakeData.CreateValidEntry() with
            {
                ProcessId = 5151,
                ProcessStartTimeUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
            };
            new RegistryFileWriter(path).Write([entry]);
            var sut = RegistryEntryReaderTestFactory.Create(path, new FakeProcessLookup());

            // Act
            var results = await sut.ReadAsync(TestContext.Current.CancellationToken);

            // Assert
            var result = Assert.Single(results);
            Assert.Equal(RegistryEntryProbeState.Stale, result.State);
        }

        [Fact]
        public async Task Should_mark_entry_Stale_when_pid_recycled_to_different_start_time()
        {
            // Arrange
            var path = tempDirectory.CreatePath(RegistryFileName);
            var entryStart = DateTimeOffset.UtcNow.AddMinutes(-10);
            var entry = RegistryEntryFakeData.CreateValidEntry() with
            {
                ProcessId = 6262,
                ProcessStartTimeUtc = entryStart,
            };
            new RegistryFileWriter(path).Write([entry]);
            var lookup = new FakeProcessLookup();
            lookup.Register(6262, new FakeProcessHandle(DateTime.UtcNow));
            var sut = RegistryEntryReaderTestFactory.Create(path, lookup);

            // Act
            var results = await sut.ReadAsync(TestContext.Current.CancellationToken);

            // Assert
            var result = Assert.Single(results);
            Assert.Equal(RegistryEntryProbeState.Stale, result.State);
        }

        [Fact]
        public async Task Should_treat_sub_second_start_time_drift_as_Live()
        {
            // Arrange
            var path = tempDirectory.CreatePath(RegistryFileName);
            var entryStart = DateTimeOffset.UtcNow.AddMinutes(-1);
            var entry = RegistryEntryFakeData.CreateValidEntry() with
            {
                ProcessId = 7373,
                ProcessStartTimeUtc = entryStart,
            };
            new RegistryFileWriter(path).Write([entry]);
            var lookup = new FakeProcessLookup();
            lookup.Register(7373, new FakeProcessHandle(entryStart.UtcDateTime.AddMilliseconds(500)));
            var sut = RegistryEntryReaderTestFactory.Create(path, lookup);

            // Act
            var results = await sut.ReadAsync(TestContext.Current.CancellationToken);

            // Assert
            var result = Assert.Single(results);
            Assert.Equal(RegistryEntryProbeState.Live, result.State);
        }

        [Fact]
        public async Task Should_classify_each_entry_independently()
        {
            // Arrange
            var path = tempDirectory.CreatePath(RegistryFileName);
            var liveStart = DateTimeOffset.UtcNow.AddMinutes(-5);
            var liveEntry = RegistryEntryFakeData.CreateValidEntry() with
            {
                ProcessId = 1001,
                ProcessStartTimeUtc = liveStart,
            };
            var staleEntry = RegistryEntryFakeData.CreateValidEntry() with
            {
                ProcessId = 1002,
                ProcessStartTimeUtc = DateTimeOffset.UtcNow.AddMinutes(-30),
            };
            new RegistryFileWriter(path).Write([liveEntry, staleEntry]);
            var lookup = new FakeProcessLookup();
            lookup.Register(1001, new FakeProcessHandle(liveStart.UtcDateTime));
            var sut = RegistryEntryReaderTestFactory.Create(path, lookup);

            // Act
            var results = await sut.ReadAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(2, results.Count),
                () => Assert.Equal(RegistryEntryProbeState.Live, results[0].State),
                () => Assert.Equal(liveEntry.InstanceId, results[0].Entry.InstanceId),
                () => Assert.Equal(RegistryEntryProbeState.Stale, results[1].State),
                () => Assert.Equal(staleEntry.InstanceId, results[1].Entry.InstanceId));
        }

        [Fact]
        public async Task Should_dispose_each_process_handle_it_opens()
        {
            // Arrange
            var path = tempDirectory.CreatePath(RegistryFileName);
            var startTime = DateTimeOffset.UtcNow.AddMinutes(-4);
            var entry = RegistryEntryFakeData.CreateValidEntry() with
            {
                ProcessId = 9999,
                ProcessStartTimeUtc = startTime,
            };
            new RegistryFileWriter(path).Write([entry]);
            var handle = new FakeProcessHandle(startTime.UtcDateTime);
            var lookup = new FakeProcessLookup();
            lookup.Register(9999, handle);
            var sut = RegistryEntryReaderTestFactory.Create(path, lookup);

            // Act
            _ = await sut.ReadAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(1, handle.DisposeCallCount);
        }
    }
}
