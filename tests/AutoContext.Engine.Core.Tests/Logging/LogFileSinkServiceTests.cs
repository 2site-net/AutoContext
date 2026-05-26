namespace AutoContext.Engine.Core.Tests.Logging;

using System.Text.Json;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Tests.Support;
using AutoContext.Engine.Core.Tests.Support.Logging;
using AutoContext.Engine.Core.Tests.Support.Shared;
using AutoContext.Engine.Protocol.Messages.Logs;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using static AutoContext.Engine.Core.Tests.Support.EngineCrashWriterFixture;

public sealed class LogFileSinkServiceTests : IClassFixture<LogFileSinkServiceFixture>
{
    private readonly LogFileSinkServiceFixture _fixture;

    public LogFileSinkServiceTests(LogFileSinkServiceFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_channel()
    {
        Assert.Throws<ArgumentNullException>(() => new LogFileSinkService(
            channel: null!,
            options: Options.Create(CreateOptions()),
            thresholds: LogRotationThresholdsFakeData.Normal,
            cleaner: RotatedLogCleanerTestFactory.Create(CreateOptions()),
            broadcaster: LogSubscriptionBroadcasterTestFactory.Create(),
            timeProvider: TimeProvider.System,
            logger: NullLogger<LogFileSinkService>.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_options()
    {
        Assert.Throws<ArgumentNullException>(() => new LogFileSinkService(
            channel: new LogChannel(),
            options: null!,
            thresholds: LogRotationThresholdsFakeData.Normal,
            cleaner: RotatedLogCleanerTestFactory.Create(CreateOptions()),
            broadcaster: LogSubscriptionBroadcasterTestFactory.Create(),
            timeProvider: TimeProvider.System,
            logger: NullLogger<LogFileSinkService>.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_thresholds()
    {
        Assert.Throws<ArgumentNullException>(() => new LogFileSinkService(
            channel: new LogChannel(),
            options: Options.Create(CreateOptions()),
            thresholds: null!,
            cleaner: RotatedLogCleanerTestFactory.Create(CreateOptions()),
            broadcaster: LogSubscriptionBroadcasterTestFactory.Create(),
            timeProvider: TimeProvider.System,
            logger: NullLogger<LogFileSinkService>.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_cleaner()
    {
        Assert.Throws<ArgumentNullException>(() => new LogFileSinkService(
            channel: new LogChannel(),
            options: Options.Create(CreateOptions()),
            thresholds: LogRotationThresholdsFakeData.Normal,
            cleaner: null!,
            broadcaster: LogSubscriptionBroadcasterTestFactory.Create(),
            timeProvider: TimeProvider.System,
            logger: NullLogger<LogFileSinkService>.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_time_provider()
    {
        Assert.Throws<ArgumentNullException>(() => new LogFileSinkService(
            channel: new LogChannel(),
            options: Options.Create(CreateOptions()),
            thresholds: LogRotationThresholdsFakeData.Normal,
            cleaner: RotatedLogCleanerTestFactory.Create(CreateOptions()),
            broadcaster: LogSubscriptionBroadcasterTestFactory.Create(),
            timeProvider: null!,
            logger: NullLogger<LogFileSinkService>.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_broadcaster()
    {
        Assert.Throws<ArgumentNullException>(() => new LogFileSinkService(
            channel: new LogChannel(),
            options: Options.Create(CreateOptions()),
            thresholds: LogRotationThresholdsFakeData.Normal,
            cleaner: RotatedLogCleanerTestFactory.Create(CreateOptions()),
            broadcaster: null!,
            timeProvider: TimeProvider.System,
            logger: NullLogger<LogFileSinkService>.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_logger()
    {
        Assert.Throws<ArgumentNullException>(() => new LogFileSinkService(
            channel: new LogChannel(),
            options: Options.Create(CreateOptions()),
            thresholds: LogRotationThresholdsFakeData.Normal,
            cleaner: RotatedLogCleanerTestFactory.Create(CreateOptions()),
            broadcaster: LogSubscriptionBroadcasterTestFactory.Create(),
            timeProvider: TimeProvider.System,
            logger: null!));
    }

    [Fact]
    public void Should_not_create_target_file_until_first_write()
    {
        // Arrange + Act
        var context = _fixture.Create();

        // Assert — neither the file nor the logs/ subdirectory
        // should exist before the drain loop receives a record.
        var expectedPath = EngineLogPathTestComposer.Compose(context.Options);
        var expectedDirectory = Path.GetDirectoryName(expectedPath);

        Assert.Multiple(
            () => Assert.False(File.Exists(expectedPath)),
            () => Assert.False(Directory.Exists(expectedDirectory)));
    }

    [Fact]
    public async Task Should_write_single_NDJSON_record_with_expected_fields()
    {
        // Arrange
        var context = _fixture.Create();
        var record = LogRecordFakeData.CreateLogRecord(
            category: "engine.test",
            level: LogLevels.Information,
            message: "hello",
            timestamp: new DateTimeOffset(2026, 4, 28, 12, 0, 0, TimeSpan.Zero));

        // Act
        await context.Service.StartAsync(TestContext.Current.CancellationToken);
        Assert.True(context.Channel.TryWrite(record));
        await context.Service.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        var records = NdjsonTestReader.Read(EngineLogPathTestComposer.Compose(context.Options));
        var single = Assert.Single(records);
        Assert.Multiple(
            () => Assert.Equal("engine.test", single.GetProperty("category").GetString()),
            () => Assert.Equal(LogLevels.Information, single.GetProperty("level").GetString()),
            () => Assert.Equal("hello", single.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task Should_drain_pending_records_in_FIFO_order_on_graceful_shutdown()
    {
        // Arrange
        var context = _fixture.Create();

        // Act — enqueue before draining starts so all three
        // records land in the buffer at once.
        Assert.True(context.Channel.TryWrite(LogRecordFakeData.CreateLogRecord(message: "first")));
        Assert.True(context.Channel.TryWrite(LogRecordFakeData.CreateLogRecord(message: "second")));
        Assert.True(context.Channel.TryWrite(LogRecordFakeData.CreateLogRecord(message: "third")));

        await context.Service.StartAsync(TestContext.Current.CancellationToken);
        await context.Service.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        var records = NdjsonTestReader.Read(EngineLogPathTestComposer.Compose(context.Options));
        Assert.Multiple(
            () => Assert.Equal(3, records.Count),
            () => Assert.Equal("first", records[0].GetProperty("message").GetString()),
            () => Assert.Equal("second", records[1].GetProperty("message").GetString()),
            () => Assert.Equal("third", records[2].GetProperty("message").GetString()));
    }

    [Fact]
    public async Task Should_create_logs_directory_lazily_when_drain_loop_opens_the_file()
    {
        // Arrange
        var context = _fixture.Create();
        var expectedPath = EngineLogPathTestComposer.Compose(context.Options);
        var expectedDirectory = Path.GetDirectoryName(expectedPath);

        // Sanity — directory must not exist before the service starts.
        Assert.False(Directory.Exists(expectedDirectory));

        // Act
        Assert.True(context.Channel.TryWrite(LogRecordFakeData.CreateLogRecord(message: "only")));
        await context.Service.StartAsync(TestContext.Current.CancellationToken);
        await context.Service.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(Directory.Exists(expectedDirectory));
        Assert.True(File.Exists(expectedPath));
    }

    [Fact]
    public async Task Should_rotate_when_line_count_threshold_reached()
    {
        // Arrange — thresholds: 2 lines, generous byte ceiling so
        // line-count is the rotation trigger.
        var rotationAt = new DateTimeOffset(2026, 5, 11, 14, 30, 52, TimeSpan.Zero);
        var clock = new FakeTimeProvider(rotationAt);
        var context = _fixture.Create(
            thresholds: new LogRotationThresholds(MaxLines: 2, MaxBytes: long.MaxValue),
            timeProvider: clock);

        // Act — three records: after the second crosses the
        // threshold and triggers rotation, the third lands in the
        // freshly opened active file.
        Assert.True(context.Channel.TryWrite(LogRecordFakeData.CreateLogRecord(message: "first")));
        Assert.True(context.Channel.TryWrite(LogRecordFakeData.CreateLogRecord(message: "second")));
        Assert.True(context.Channel.TryWrite(LogRecordFakeData.CreateLogRecord(message: "third")));

        await context.Service.StartAsync(TestContext.Current.CancellationToken);
        await context.Service.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        var directory = Path.GetDirectoryName(EngineLogPathTestComposer.Compose(context.Options))!;
        var rotated = Directory.GetFiles(directory, "engine-*.log");
        var activeRecords = NdjsonTestReader.Read(EngineLogPathTestComposer.Compose(context.Options));
        var rotatedRecords = rotated.Length == 1 ? NdjsonTestReader.Read(rotated[0]) : [];

        Assert.Multiple(
            () => Assert.Single(rotated),
            () => Assert.Equal(2, rotatedRecords.Count),
            () => Assert.Equal("first", rotatedRecords[0].GetProperty("message").GetString()),
            () => Assert.Equal("second", rotatedRecords[1].GetProperty("message").GetString()),
            () => Assert.Single(activeRecords),
            () => Assert.Equal("third", activeRecords[0].GetProperty("message").GetString()));
    }

    [Fact]
    public async Task Should_rotate_when_byte_threshold_reached()
    {
        // Arrange — a one-byte ceiling guarantees rotation after
        // every single record (even the smallest payload exceeds
        // 1 byte once serialised + newline-terminated).
        var clock = new FakeTimeProvider(
            new DateTimeOffset(2026, 5, 11, 14, 30, 52, TimeSpan.Zero));
        var context = _fixture.Create(
            thresholds: new LogRotationThresholds(MaxLines: int.MaxValue, MaxBytes: 1),
            timeProvider: clock);

        // Act — single record + shutdown is enough; the byte
        // ceiling fires the rotation as soon as the first record
        // is flushed.
        Assert.True(context.Channel.TryWrite(LogRecordFakeData.CreateLogRecord(message: "only")));

        await context.Service.StartAsync(TestContext.Current.CancellationToken);
        await context.Service.StopAsync(TestContext.Current.CancellationToken);

        // Assert — rotated file holds the record; the active file
        // was reopened empty and stays empty (no further records).
        var directory = Path.GetDirectoryName(EngineLogPathTestComposer.Compose(context.Options))!;
        var rotated = Directory.GetFiles(directory, "engine-*.log");
        var rotatedRecords = rotated.Length == 1 ? NdjsonTestReader.Read(rotated[0]) : [];
        var activeRecords = NdjsonTestReader.Read(EngineLogPathTestComposer.Compose(context.Options));

        Assert.Multiple(
            () => Assert.Single(rotated),
            () => Assert.Single(rotatedRecords),
            () => Assert.Equal("only", rotatedRecords[0].GetProperty("message").GetString()),
            () => Assert.Empty(activeRecords));
    }

    [Fact]
    public async Task Should_name_rotated_file_with_basic_iso8601_utc_timestamp()
    {
        // Arrange
        var rotationAt = new DateTimeOffset(2026, 5, 11, 14, 30, 52, TimeSpan.Zero);
        var clock = new FakeTimeProvider(rotationAt);
        var context = _fixture.Create(
            thresholds: new LogRotationThresholds(MaxLines: 1, MaxBytes: long.MaxValue),
            timeProvider: clock);

        // Act
        Assert.True(context.Channel.TryWrite(LogRecordFakeData.CreateLogRecord(message: "first")));
        await context.Service.StartAsync(TestContext.Current.CancellationToken);
        await context.Service.StopAsync(TestContext.Current.CancellationToken);

        // Assert — rotated filename must use the basic ISO 8601
        // UTC pattern (no colons, trailing Z) and match the clock.
        var directory = Path.GetDirectoryName(EngineLogPathTestComposer.Compose(context.Options))!;
        var rotated = Assert.Single(Directory.GetFiles(directory, "engine-*.log"));
        var fileName = Path.GetFileName(rotated);

        Assert.Equal("engine-20260511T143052Z.log", fileName);
    }

    [Fact]
    public async Task Should_invoke_cleaner_after_rotation_and_delete_expired_rotated_siblings()
    {
        // Arrange — pre-seed an "old" rotated file whose
        // filename timestamp falls outside the retention window,
        // then drive one rotation event and verify the old
        // sibling is gone while the just-rotated one survives.
        var options = CreateOptions();
        options.Retention = TimeSpan.FromMinutes(5);
        var rotationAt = new DateTimeOffset(2026, 5, 11, 14, 30, 52, TimeSpan.Zero);
        var clock = new FakeTimeProvider(rotationAt);

        var expectedActivePath = EngineLogPathTestComposer.Compose(options);
        var directory = Path.GetDirectoryName(expectedActivePath)!;
        Directory.CreateDirectory(directory);

        // "Old" rotated sibling stamped one hour before "now".
        var oldRotatedName = RotatedLogCleaner.ComposeRotatedFileName(
            "engine",
            rotationAt - TimeSpan.FromHours(1));
        var oldRotatedPath = Path.Combine(directory, oldRotatedName);
        await File.WriteAllTextAsync(oldRotatedPath, "{}\n", TestContext.Current.CancellationToken);

        var context = _fixture.Create(
            options: options,
            thresholds: new LogRotationThresholds(MaxLines: 1, MaxBytes: long.MaxValue),
            timeProvider: clock);

        // Act
        Assert.True(context.Channel.TryWrite(LogRecordFakeData.CreateLogRecord(message: "trigger")));
        await context.Service.StartAsync(TestContext.Current.CancellationToken);
        await context.Service.StopAsync(TestContext.Current.CancellationToken);

        // Assert — old sibling deleted; freshly rotated file
        // (timestamped "now") preserved.
        var freshRotatedPath = Path.Combine(
            directory,
            RotatedLogCleaner.ComposeRotatedFileName("engine", rotationAt));

        Assert.Multiple(
            () => Assert.False(File.Exists(oldRotatedPath)),
            () => Assert.True(File.Exists(freshRotatedPath)));
    }

    [Fact]
    public async Task Should_fan_out_drained_record_to_live_subscriber_and_file()
    {
        // Arrange — a single shared broadcaster wired into the
        // sink service; the subscriber is created up-front so it
        // receives the record alongside the file sink.
        var context = _fixture.Create();
        using var subscriber = context.Broadcaster.Subscribe();

        // Act — write one record, then drive a graceful shutdown
        // so the broadcaster completes and the subscriber's
        // ReadAllAsync exits cleanly.
        await context.Service.StartAsync(TestContext.Current.CancellationToken);
        Assert.True(context.Channel.TryWrite(LogRecordFakeData.CreateLogRecord(message: "fan-out")));
        await context.Service.StopAsync(TestContext.Current.CancellationToken);

        var frames = await LogSubscriptionTestDrainer.DrainAsync(subscriber);

        // Assert — file sink and subscriber both observed the
        // same record; the subscriber stream ended via EOF, with
        // no terminal evicted frame.
        var fileRecords = NdjsonTestReader.Read(EngineLogPathTestComposer.Compose(context.Options));
        var single = Assert.Single(frames);
        var recordFrame = Assert.IsType<LogRecordFrame>(single);
        Assert.Multiple(
            () => Assert.Equal("fan-out", recordFrame.Record.Message),
            () => Assert.Single(fileRecords),
            () => Assert.Equal("fan-out", fileRecords[0].GetProperty("message").GetString()));
    }

    [Fact]
    public async Task Should_complete_broadcaster_on_graceful_shutdown()
    {
        // Arrange
        var context = _fixture.Create();
        using var subscriber = context.Broadcaster.Subscribe();

        // Act — start, then immediately stop. No records are
        // published; the broadcaster must still complete so the
        // subscriber's enumerator terminates without hanging.
        await context.Service.StartAsync(TestContext.Current.CancellationToken);
        await context.Service.StopAsync(TestContext.Current.CancellationToken);

        var frames = await LogSubscriptionTestDrainer.DrainAsync(subscriber);

        // Assert — clean EOF, no terminal evicted frame.
        Assert.Empty(frames);
    }
}
