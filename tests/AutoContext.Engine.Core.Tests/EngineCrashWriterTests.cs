namespace AutoContext.Engine.Core.Tests;

using System.Text.Json;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Infrastructure.Primitives;
using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Core.Tests.Support;

using static AutoContext.Engine.Core.Tests.Support.EngineCrashWriterFixture;

public sealed class EngineCrashWriterTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_options()
    {
        Assert.Throws<ArgumentNullException>(() => new EngineCrashWriter(null!));
    }

    [Fact]
    public void Should_compose_crash_log_path_under_per_instance_subtree()
    {
        var cacheRoot = CreateTempCacheRoot();
        var options = CreateOptions(cacheRoot);
        var writer = CreateWriter(options);

        var expectedHash = WorkspaceHash.Compute(options.WorkspacePath).Value;
        var expected = Path.Combine(
            cacheRoot,
            expectedHash,
            options.InstanceId.ToString("D"),
            EngineCrashWriter.LogsSubdirectory,
            EngineCrashWriter.CrashLogFileName);

        Assert.Equal(expected, writer.CrashLogPath);
    }

    [Fact]
    public void Should_not_create_target_file_until_first_write()
    {
        var writer = CreateWriter(CreateOptions());

        Assert.False(File.Exists(writer.CrashLogPath));
    }

    [Fact]
    public void Should_write_single_NDJSON_record_with_expected_fields()
    {
        var options = CreateOptions();
        var writer = CreateWriter(options);
        var exception = new InvalidOperationException("boom");

        writer.TryWrite(exception, "DaemonHostFactory.RunAsync");

        var records = ReadRecords(writer);
        Assert.Single(records);
        var record = records[0];
        Assert.Multiple(
            () => Assert.Equal("DaemonHostFactory.RunAsync", record.GetProperty("Source").GetString()),
            () => Assert.Equal(options.InstanceId.ToString("D"), record.GetProperty("InstanceId").GetString()),
            () => Assert.Equal(options.WorkspacePath, record.GetProperty("WorkspacePath").GetString()),
            () => Assert.Equal(typeof(InvalidOperationException).FullName, record.GetProperty("Exception").GetProperty("Type").GetString()),
            () => Assert.Equal("boom", record.GetProperty("Exception").GetProperty("Message").GetString()),
            () => Assert.True(DateTimeOffset.TryParse(record.GetProperty("Timestamp").GetString(), out _)));
    }

    [Fact]
    public void Should_append_records_when_called_multiple_times()
    {
        var writer = CreateWriter(CreateOptions());

        writer.TryWrite(new InvalidOperationException("first"), "AppDomain.UnhandledException");
        writer.TryWrite(new IOException("second"), "TaskScheduler.UnobservedTaskException");
        writer.TryWrite(new ArgumentException("third"), "DaemonHostFactory.RunAsync");

        var records = ReadRecords(writer);
        Assert.Multiple(
            () => Assert.Equal(3, records.Count),
            () => Assert.Equal("AppDomain.UnhandledException", records[0].GetProperty("Source").GetString()),
            () => Assert.Equal("TaskScheduler.UnobservedTaskException", records[1].GetProperty("Source").GetString()),
            () => Assert.Equal("DaemonHostFactory.RunAsync", records[2].GetProperty("Source").GetString()),
            () => Assert.Equal("first", records[0].GetProperty("Exception").GetProperty("Message").GetString()),
            () => Assert.Equal("second", records[1].GetProperty("Exception").GetProperty("Message").GetString()),
            () => Assert.Equal("third", records[2].GetProperty("Exception").GetProperty("Message").GetString()));
    }

    [Fact]
    public void Should_capture_inner_exceptions_recursively()
    {
        var writer = CreateWriter(CreateOptions());
        var root = new IOException("disk full");
        var middle = new InvalidOperationException("write failed", root);
        var outer = new AggregateException("operation faulted", middle);

        writer.TryWrite(outer, "DaemonHostFactory.RunAsync");

        var record = Assert.Single(ReadRecords(writer));
        var exception = record.GetProperty("Exception");
        var inner = exception.GetProperty("Inner");
        var innermost = inner.GetProperty("Inner");
        Assert.Multiple(
            () => Assert.Equal(typeof(AggregateException).FullName, exception.GetProperty("Type").GetString()),
            () => Assert.Equal(typeof(InvalidOperationException).FullName, inner.GetProperty("Type").GetString()),
            () => Assert.Equal("write failed", inner.GetProperty("Message").GetString()),
            () => Assert.Equal(typeof(IOException).FullName, innermost.GetProperty("Type").GetString()),
            () => Assert.Equal("disk full", innermost.GetProperty("Message").GetString()),
            () => Assert.Equal(JsonValueKind.Null, innermost.GetProperty("Inner").ValueKind));
    }

    [Fact]
    public void Should_return_silently_when_exception_is_null()
    {
        var writer = CreateWriter(CreateOptions());

        writer.TryWrite(null!, "DaemonHostFactory.RunAsync");

        Assert.False(File.Exists(writer.CrashLogPath));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Should_return_silently_when_source_is_null_or_empty(string? source)
    {
        var writer = CreateWriter(CreateOptions());

        writer.TryWrite(new InvalidOperationException("boom"), source!);

        Assert.False(File.Exists(writer.CrashLogPath));
    }

    [Fact]
    public void Should_swallow_IO_failures_so_original_fault_is_not_masked()
    {
        // Arrange: place a regular FILE where the writer expects
        // to create the `logs` directory. Directory.CreateDirectory
        // will throw IOException ("a file with the same name and
        // location already exists"), which the writer must swallow.
        var cacheRoot = CreateTempCacheRoot();
        var options = CreateOptions(cacheRoot);
        var writer = CreateWriter(options);
        var logsDirectoryPath = Path.GetDirectoryName(writer.CrashLogPath)!;
        var parentDirectory = Path.GetDirectoryName(logsDirectoryPath)!;
        Directory.CreateDirectory(parentDirectory);
        File.WriteAllText(logsDirectoryPath, "blocking file");

        // Act + assert: must not throw.
        var ex = Record.Exception(() => writer.TryWrite(new InvalidOperationException("boom"), "DaemonHostFactory.RunAsync"));

        Assert.Multiple(
            () => Assert.Null(ex),
            () => Assert.False(File.Exists(writer.CrashLogPath)));
    }
}
