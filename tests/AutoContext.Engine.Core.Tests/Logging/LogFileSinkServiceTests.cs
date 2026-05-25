namespace AutoContext.Engine.Core.Tests.Logging;

using System.Text.Json;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Infrastructure.Primitives;
using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Tests.Support;
using AutoContext.Engine.Protocol.Messages.Logs;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using static AutoContext.Engine.Core.Tests.Support.EngineCrashWriterFixture;

public sealed class LogFileSinkServiceTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_channel()
    {
        Assert.Throws<ArgumentNullException>(() => new LogFileSinkService(
            channel: null!,
            options: Options.Create(CreateOptions()),
            logger: NullLogger<LogFileSinkService>.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_options()
    {
        Assert.Throws<ArgumentNullException>(() => new LogFileSinkService(
            channel: new LogChannel(),
            options: null!,
            logger: NullLogger<LogFileSinkService>.Instance));
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_logger()
    {
        Assert.Throws<ArgumentNullException>(() => new LogFileSinkService(
            channel: new LogChannel(),
            options: Options.Create(CreateOptions()),
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
        using var service = new LogFileSinkService(
            channel,
            Options.Create(options),
            NullLogger<LogFileSinkService>.Instance);
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
        using var service = new LogFileSinkService(
            channel,
            Options.Create(options),
            NullLogger<LogFileSinkService>.Instance);

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
        using var service = new LogFileSinkService(
            channel,
            Options.Create(options),
            NullLogger<LogFileSinkService>.Instance);

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
        new(
            new LogChannel(),
            Options.Create(options),
            NullLogger<LogFileSinkService>.Instance);

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
