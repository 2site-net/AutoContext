namespace AutoContext.Engine.Core.Tests.Logging;

using System.Text.Json;

using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Tests.Support;
using AutoContext.Engine.Core.Tests.Support.Logging;
using AutoContext.Engine.Core.Tests.Support.Machine;
using AutoContext.Engine.Protocol.Messages.Logs;
using AutoContext.Engine.Protocol.Serialization;

using Microsoft.Extensions.Options;

public sealed class EngineLogFileReaderTests : IDisposable
{
    private readonly string _cacheRoot = EngineCrashWriterFixture.CreateTempCacheRoot();

    public void Dispose()
    {
        if (Directory.Exists(_cacheRoot))
        {
            Directory.Delete(_cacheRoot, recursive: true);
        }
    }

    [Fact]
    public void Should_throw_when_constructed_with_null_paths()
    {
        Assert.Throws<ArgumentNullException>(() => new EngineLogFileReader(null!));
    }

    [Fact]
    public async Task Should_return_empty_when_file_does_not_exist()
    {
        var (reader, _) = CreateReader();

        var result = await reader.ReadAsync(
            parameters: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.Empty(result.Records),
            () => Assert.False(result.Truncated));
    }

    [Fact]
    public async Task Should_return_all_records_when_no_filters_supplied()
    {
        var (reader, path) = CreateReader();
        await WriteRecordsAsync(path,
            CreateRecord(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "first"),
            CreateRecord(new DateTimeOffset(2026, 1, 1, 0, 0, 1, TimeSpan.Zero), "second"),
            CreateRecord(new DateTimeOffset(2026, 1, 1, 0, 0, 2, TimeSpan.Zero), "third"));

        var result = await reader.ReadAsync(
            parameters: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.Equal(3, result.Records.Count),
            () => Assert.Equal("first", result.Records[0].Message),
            () => Assert.Equal("third", result.Records[2].Message),
            () => Assert.False(result.Truncated));
    }

    [Fact]
    public async Task Should_skip_malformed_lines_silently()
    {
        var (reader, path) = CreateReader();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var good = JsonSerializer.Serialize(
            CreateRecord(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "ok"),
            ProtocolJsonContext.Default.LogRecord);
        await File.WriteAllTextAsync(
            path,
            $"{good}\n{{not valid json\n{good}\n",
            TestContext.Current.CancellationToken);

        var result = await reader.ReadAsync(
            parameters: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.Equal(2, result.Records.Count),
            () => Assert.False(result.Truncated));
    }

    [Fact]
    public async Task Should_apply_since_filter_dropping_older_records()
    {
        var (reader, path) = CreateReader();
        await WriteRecordsAsync(path,
            CreateRecord(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "old"),
            CreateRecord(new DateTimeOffset(2026, 1, 1, 0, 0, 5, TimeSpan.Zero), "kept-1"),
            CreateRecord(new DateTimeOffset(2026, 1, 1, 0, 0, 10, TimeSpan.Zero), "kept-2"));

        var result = await reader.ReadAsync(
            new LogsGetEngineParams
            {
                Since = new DateTimeOffset(2026, 1, 1, 0, 0, 3, TimeSpan.Zero),
            },
            TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.Equal(2, result.Records.Count),
            () => Assert.Equal("kept-1", result.Records[0].Message),
            () => Assert.False(result.Truncated));
    }

    [Fact]
    public async Task Should_report_truncated_when_active_file_starts_after_since_cutoff()
    {
        var (reader, path) = CreateReader();
        await WriteRecordsAsync(path,
            CreateRecord(new DateTimeOffset(2026, 1, 1, 0, 0, 10, TimeSpan.Zero), "a"),
            CreateRecord(new DateTimeOffset(2026, 1, 1, 0, 0, 11, TimeSpan.Zero), "b"));

        // Since predates the file's earliest record → records that
        // would satisfy the request rotated past the active file.
        var result = await reader.ReadAsync(
            new LogsGetEngineParams
            {
                Since = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            },
            TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.Equal(2, result.Records.Count),
            () => Assert.True(result.Truncated));
    }

    [Fact]
    public async Task Should_return_last_N_records_in_chronological_order()
    {
        var (reader, path) = CreateReader();
        await WriteRecordsAsync(path,
            CreateRecord(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "1"),
            CreateRecord(new DateTimeOffset(2026, 1, 1, 0, 0, 1, TimeSpan.Zero), "2"),
            CreateRecord(new DateTimeOffset(2026, 1, 1, 0, 0, 2, TimeSpan.Zero), "3"),
            CreateRecord(new DateTimeOffset(2026, 1, 1, 0, 0, 3, TimeSpan.Zero), "4"));

        var result = await reader.ReadAsync(
            new LogsGetEngineParams { LastN = 2 },
            TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.Equal(2, result.Records.Count),
            () => Assert.Equal("3", result.Records[0].Message),
            () => Assert.Equal("4", result.Records[1].Message),
            () => Assert.False(result.Truncated));
    }

    [Fact]
    public async Task Should_return_empty_records_but_still_compute_truncated_when_lastN_is_zero()
    {
        var (reader, path) = CreateReader();
        await WriteRecordsAsync(path,
            CreateRecord(new DateTimeOffset(2026, 1, 1, 0, 0, 5, TimeSpan.Zero), "a"));

        var result = await reader.ReadAsync(
            new LogsGetEngineParams
            {
                LastN = 0,
                Since = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            },
            TestContext.Current.CancellationToken);

        Assert.Multiple(
            () => Assert.Empty(result.Records),
            () => Assert.True(result.Truncated));
    }

    [Fact]
    public async Task Should_short_circuit_without_reading_file_when_lastN_is_zero_and_since_is_null()
    {
        // Arrange — point the reader at a path that does NOT
        // exist. With LastN=0 and no Since cutoff, truncated is
        // definitionally false and the reader must not touch
        // disk at all.
        var (reader, _) = CreateReader();

        // Act
        var result = await reader.ReadAsync(
            new LogsGetEngineParams { LastN = 0 },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Empty(result.Records),
            () => Assert.False(result.Truncated));
    }

    [Fact]
    public async Task Should_throw_when_lastN_is_negative()
    {
        var (reader, _) = CreateReader();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => reader.ReadAsync(
                new LogsGetEngineParams { LastN = -1 },
                TestContext.Current.CancellationToken));
    }

    private (EngineLogFileReader Reader, string FilePath) CreateReader()
    {
        var options = EngineCrashWriterFixture.CreateOptions(_cacheRoot);
        var paths = new EngineLogPaths(Options.Create(options));
        return (new EngineLogFileReader(paths), paths.EngineLogFilePath);
    }

    private static LogRecord CreateRecord(DateTimeOffset timestamp, string message) =>
        new()
        {
            Timestamp = timestamp,
            Category = "AutoContext.Tests",
            Level = "Information",
            Message = message,
        };

    private static async Task WriteRecordsAsync(string path, params LogRecord[] records)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var writer = new StreamWriter(path, append: false);
        foreach (var record in records)
        {
            var line = JsonSerializer.Serialize(
                record, ProtocolJsonContext.Default.LogRecord);
            await writer.WriteLineAsync(line);
        }
    }
}
