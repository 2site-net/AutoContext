namespace AutoContext.Engine.Core.Tests.Logging;

using System.Text.Json;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Infrastructure.Primitives;
using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Tests.Support;
using AutoContext.Engine.Core.Tests.Support.Shared;
using AutoContext.Engine.Protocol.Messages.Logs;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using static AutoContext.Engine.Core.Tests.Support.EngineCrashWriterFixture;

public sealed class LogFileSinkServiceTests
{
    private static readonly LogRotationThresholds NormalThresholds =
        LogRotationThresholds.ForVerbosity(EngineLoggingVerbosity.Normal);

    [Fact]
    public void Should_throw_when_constructed_with_null_channel()
    {
        Assert.Throws<ArgumentNullException>(() => new LogFileSinkService(
            channel: null!,
            options: Options.Create(CreateOptions()),
            thresholds: NormalThresholds,
            cleaner: CreateCleaner(CreateOptions()),
            timeProvider: TimeProvider.System,
            logger: NullLogger<LogFileSinkService>.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_options()
    {
        Assert.Throws<ArgumentNullException>(() => new LogFileSinkService(
            channel: new LogChannel(),
            options: null!,
            thresholds: NormalThresholds,
            cleaner: CreateCleaner(CreateOptions()),
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
            cleaner: CreateCleaner(CreateOptions()),
            timeProvider: TimeProvider.System,
            logger: NullLogger<LogFileSinkService>.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_cleaner()
    {
        Assert.Throws<ArgumentNullException>(() => new LogFileSinkService(
            channel: new LogChannel(),
            options: Options.Create(CreateOptions()),
            thresholds: NormalThresholds,
            cleaner: null!,
            timeProvider: TimeProvider.System,
            logger: NullLogger<LogFileSinkService>.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_time_provider()
    {
        Assert.Throws<ArgumentNullException>(() => new LogFileSinkService(
            channel: new LogChannel(),
            options: Options.Create(CreateOptions()),
            thresholds: NormalThresholds,
            cleaner: CreateCleaner(CreateOptions()),
            timeProvider: null!,
            logger: NullLogger<LogFileSinkService>.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_logger()
    {
        Assert.Throws<ArgumentNullException>(() => new LogFileSinkService(
            channel: new LogChannel(),
            options: Options.Create(CreateOptions()),
            thresholds: NormalThresholds,
            cleaner: CreateCleaner(CreateOptions()),
            timeProvider: TimeProvider.System,
            logger: null!));
    }

    [Fact]
    public void Should_not_create_target_file_until_first_write()
    {
        // Arrange
        var cacheRoot = CreateTempCacheRoot();
        var options = CreateOptions(cacheRoot);

        // Act
        using var service = CreateService(options);

        // Assert — neither the file nor the logs/ subdirectory
        // should exist before the drain loop receives a record.
        var expectedPath = ComposeExpectedLogPath(options);
        var expectedDirectory = Path.GetDirectoryName(expectedPath);

        Assert.Multiple(
            () => Assert.False(File.Exists(expectedPath)),
            () => Assert.False(Directory.Exists(expectedDirectory)));
    }

    [Fact]
    public async Task Should_write_single_NDJSON_record_with_expected_fields()
    {
        // Arrange
        var cacheRoot = CreateTempCacheRoot();
        var options = CreateOptions(cacheRoot);
        var channel = new LogChannel();
        using var service = CreateService(options, channel);
        var record = new LogRecord
        {
            Timestamp = new DateTimeOffset(2026, 4, 28, 12, 0, 0, TimeSpan.Zero),
            Category = "engine.test",
            Level = LogLevels.Information,
            Message = "hello",
        };

        // Act
        await service.StartAsync(TestContext.Current.CancellationToken);
        Assert.True(channel.TryWrite(record));
        await service.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        var records = ReadNdjson(ComposeExpectedLogPath(options));
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
        var cacheRoot = CreateTempCacheRoot();
        var options = CreateOptions(cacheRoot);
        var channel = new LogChannel();
        using var service = CreateService(options, channel);

        // Act — enqueue before draining starts so all three
        // records land in the buffer at once.
        Assert.True(channel.TryWrite(CreateRecord("first")));
        Assert.True(channel.TryWrite(CreateRecord("second")));
        Assert.True(channel.TryWrite(CreateRecord("third")));

        await service.StartAsync(TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        var records = ReadNdjson(ComposeExpectedLogPath(options));
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
        var cacheRoot = CreateTempCacheRoot();
        var options = CreateOptions(cacheRoot);
        var channel = new LogChannel();
        using var service = CreateService(options, channel);

        var expectedPath = ComposeExpectedLogPath(options);
        var expectedDirectory = Path.GetDirectoryName(expectedPath);

        // Sanity — directory must not exist before the service starts.
        Assert.False(Directory.Exists(expectedDirectory));

        // Act
        Assert.True(channel.TryWrite(CreateRecord("only")));
        await service.StartAsync(TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(Directory.Exists(expectedDirectory));
        Assert.True(File.Exists(expectedPath));
    }

    [Fact]
    public async Task Should_rotate_when_line_count_threshold_reached()
    {
        // Arrange — thresholds: 2 lines, generous byte ceiling so
        // line-count is the rotation trigger.
        var cacheRoot = CreateTempCacheRoot();
        var options = CreateOptions(cacheRoot);
        var rotationAt = new DateTimeOffset(2026, 5, 11, 14, 30, 52, TimeSpan.Zero);
        var clock = new FakeTimeProvider(rotationAt);
        var channel = new LogChannel();
        var thresholds = new LogRotationThresholds(MaxLines: 2, MaxBytes: long.MaxValue);
        using var service = new LogFileSinkService(
            channel,
            Options.Create(options),
            thresholds,
            CreateCleaner(options, clock),
            clock,
            NullLogger<LogFileSinkService>.Instance);

        // Act — three records: after the second crosses the
        // threshold and triggers rotation, the third lands in the
        // freshly opened active file.
        Assert.True(channel.TryWrite(CreateRecord("first")));
        Assert.True(channel.TryWrite(CreateRecord("second")));
        Assert.True(channel.TryWrite(CreateRecord("third")));

        await service.StartAsync(TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        var directory = Path.GetDirectoryName(ComposeExpectedLogPath(options))!;
        var rotated = Directory.GetFiles(directory, "engine-*.log");
        var activeRecords = ReadNdjson(ComposeExpectedLogPath(options));
        var rotatedRecords = rotated.Length == 1 ? ReadNdjson(rotated[0]) : [];

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
        var cacheRoot = CreateTempCacheRoot();
        var options = CreateOptions(cacheRoot);
        var clock = new FakeTimeProvider(
            new DateTimeOffset(2026, 5, 11, 14, 30, 52, TimeSpan.Zero));
        var channel = new LogChannel();
        var thresholds = new LogRotationThresholds(MaxLines: int.MaxValue, MaxBytes: 1);
        using var service = new LogFileSinkService(
            channel,
            Options.Create(options),
            thresholds,
            CreateCleaner(options, clock),
            clock,
            NullLogger<LogFileSinkService>.Instance);

        // Act — single record + shutdown is enough; the byte
        // ceiling fires the rotation as soon as the first record
        // is flushed.
        Assert.True(channel.TryWrite(CreateRecord("only")));

        await service.StartAsync(TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        // Assert — rotated file holds the record; the active file
        // was reopened empty and stays empty (no further records).
        var directory = Path.GetDirectoryName(ComposeExpectedLogPath(options))!;
        var rotated = Directory.GetFiles(directory, "engine-*.log");
        var rotatedRecords = rotated.Length == 1 ? ReadNdjson(rotated[0]) : [];
        var activeRecords = ReadNdjson(ComposeExpectedLogPath(options));

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
        var cacheRoot = CreateTempCacheRoot();
        var options = CreateOptions(cacheRoot);
        var rotationAt = new DateTimeOffset(2026, 5, 11, 14, 30, 52, TimeSpan.Zero);
        var clock = new FakeTimeProvider(rotationAt);
        var channel = new LogChannel();
        var thresholds = new LogRotationThresholds(MaxLines: 1, MaxBytes: long.MaxValue);
        using var service = new LogFileSinkService(
            channel,
            Options.Create(options),
            thresholds,
            CreateCleaner(options, clock),
            clock,
            NullLogger<LogFileSinkService>.Instance);

        // Act
        Assert.True(channel.TryWrite(CreateRecord("first")));
        await service.StartAsync(TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        // Assert — rotated filename must use the basic ISO 8601
        // UTC pattern (no colons, trailing Z) and match the clock.
        var directory = Path.GetDirectoryName(ComposeExpectedLogPath(options))!;
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
        var cacheRoot = CreateTempCacheRoot();
        var options = CreateOptions(cacheRoot);
        options.Retention = TimeSpan.FromMinutes(5);
        var rotationAt = new DateTimeOffset(2026, 5, 11, 14, 30, 52, TimeSpan.Zero);
        var clock = new FakeTimeProvider(rotationAt);

        var expectedActivePath = ComposeExpectedLogPath(options);
        var directory = Path.GetDirectoryName(expectedActivePath)!;
        Directory.CreateDirectory(directory);

        // "Old" rotated sibling stamped one hour before "now".
        var oldRotatedName = RotatedLogCleaner.ComposeRotatedFileName(
            "engine",
            rotationAt - TimeSpan.FromHours(1));
        var oldRotatedPath = Path.Combine(directory, oldRotatedName);
        await File.WriteAllTextAsync(oldRotatedPath, "{}\n", TestContext.Current.CancellationToken);

        var channel = new LogChannel();
        var thresholds = new LogRotationThresholds(MaxLines: 1, MaxBytes: long.MaxValue);
        using var service = new LogFileSinkService(
            channel,
            Options.Create(options),
            thresholds,
            CreateCleaner(options, clock),
            clock,
            NullLogger<LogFileSinkService>.Instance);

        // Act
        Assert.True(channel.TryWrite(CreateRecord("trigger")));
        await service.StartAsync(TestContext.Current.CancellationToken);
        await service.StopAsync(TestContext.Current.CancellationToken);

        // Assert — old sibling deleted; freshly rotated file
        // (timestamped "now") preserved.
        var freshRotatedPath = Path.Combine(
            directory,
            RotatedLogCleaner.ComposeRotatedFileName("engine", rotationAt));

        Assert.Multiple(
            () => Assert.False(File.Exists(oldRotatedPath)),
            () => Assert.True(File.Exists(freshRotatedPath)));
    }

    private static string ComposeExpectedLogPath(EngineOptions options)
    {
        // Every test in this file constructs options via
        // CreateOptions(cacheRoot) where cacheRoot is set, so
        // CacheRootOverride is the effective cache root and we
        // can compose the expected path without depending on
        // EngineCacheRoot.Resolve (which is internal to the
        // engine-core assembly).
        var cacheRoot = options.CacheRootOverride
            ?? throw new InvalidOperationException(
                "Tests in this class must construct EngineOptions with a non-null CacheRootOverride.");
        var workspaceHash = WorkspaceHash.Compute(options.WorkspacePath).Value;

        return Path.Combine(
            cacheRoot,
            workspaceHash,
            options.InstanceId.ToString("D"),
            EngineCrashWriter.LogsSubdirectory,
            LogFileSinkService.EngineLogFileName);
    }

    private static LogFileSinkService CreateService(EngineOptions options) =>
        CreateService(options, new LogChannel());

    private static LogFileSinkService CreateService(EngineOptions options, LogChannel channel) =>
        new(
            channel,
            Options.Create(options),
            NormalThresholds,
            CreateCleaner(options),
            TimeProvider.System,
            NullLogger<LogFileSinkService>.Instance);

    private static RotatedLogCleaner CreateCleaner(EngineOptions options) =>
        CreateCleaner(options, TimeProvider.System);

    private static RotatedLogCleaner CreateCleaner(EngineOptions options, TimeProvider clock) =>
        new(
            new RetentionPolicy(Options.Create(options), clock),
            NullLogger<RotatedLogCleaner>.Instance);

    private static LogRecord CreateRecord(string message) =>
        new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            Category = "engine.test",
            Level = LogLevels.Information,
            Message = message,
        };

    private static List<JsonElement> ReadNdjson(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var lines = File.ReadAllLines(path);
        var records = new List<JsonElement>(lines.Length);
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                records.Add(JsonDocument.Parse(line).RootElement.Clone());
            }
        }

        return records;
    }
}
