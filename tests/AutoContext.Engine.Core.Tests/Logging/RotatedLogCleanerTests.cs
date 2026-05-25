namespace AutoContext.Engine.Core.Tests.Logging;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Logging;
using AutoContext.Engine.Core.Tests.Support.Shared;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

public sealed class RotatedLogCleanerTests
{
    private const string BaseName = "engine";

    private static readonly DateTimeOffset KnownNow =
        new(2026, 5, 11, 14, 30, 52, TimeSpan.Zero);

    [Fact]
    public void Should_throw_when_constructed_with_null_retention_policy() =>
        Assert.Throws<ArgumentNullException>(() => new RotatedLogCleaner(
            retentionPolicy: null!,
            logger: NullLogger<RotatedLogCleaner>.Instance));

    [Fact]
    public void Should_throw_when_constructed_with_null_logger() =>
        Assert.Throws<ArgumentNullException>(() => new RotatedLogCleaner(
            CreatePolicy(TimeSpan.FromHours(1)),
            logger: null!));

    [Fact]
    public void ComposeRotatedFileName_should_use_basic_iso8601_utc_format()
    {
        // Arrange
        var stamp = new DateTimeOffset(2026, 5, 11, 14, 30, 52, TimeSpan.Zero);

        // Act
        var name = RotatedLogCleaner.ComposeRotatedFileName(BaseName, stamp);

        // Assert
        Assert.Equal("engine-20260511T143052Z.log", name);
    }

    [Fact]
    public void DeleteExpired_should_delete_files_outside_retention_window()
    {
        // Arrange
        using var directory = new TempDirectory();
        var policy = CreatePolicy(TimeSpan.FromMinutes(10));
        var cleaner = new RotatedLogCleaner(policy, NullLogger<RotatedLogCleaner>.Instance);

        var oldPath = SeedRotatedFile(directory.Path, KnownNow - TimeSpan.FromHours(1));

        // Act
        cleaner.DeleteExpired(directory.Path, BaseName);

        // Assert
        Assert.False(File.Exists(oldPath));
    }

    [Fact]
    public void DeleteExpired_should_preserve_files_inside_retention_window()
    {
        // Arrange
        using var directory = new TempDirectory();
        var policy = CreatePolicy(TimeSpan.FromHours(1));
        var cleaner = new RotatedLogCleaner(policy, NullLogger<RotatedLogCleaner>.Instance);

        var freshPath = SeedRotatedFile(directory.Path, KnownNow - TimeSpan.FromMinutes(5));

        // Act
        cleaner.DeleteExpired(directory.Path, BaseName);

        // Assert
        Assert.True(File.Exists(freshPath));
    }

    [Fact]
    public void DeleteExpired_should_skip_active_log_file()
    {
        // Arrange — `engine.log` (without timestamp) is the
        // active file and must never be touched by the sweeper.
        using var directory = new TempDirectory();
        var policy = CreatePolicy(TimeSpan.Zero);
        var cleaner = new RotatedLogCleaner(policy, NullLogger<RotatedLogCleaner>.Instance);

        var activePath = Path.Combine(directory.Path, "engine.log");
        File.WriteAllText(activePath, "{}\n");

        // Act
        cleaner.DeleteExpired(directory.Path, BaseName);

        // Assert
        Assert.True(File.Exists(activePath));
    }

    [Fact]
    public void DeleteExpired_should_ignore_files_with_unrelated_basename()
    {
        // Arrange — files whose basename does not match
        // `engine-` (e.g. a future `worker-{id}-...` file) are
        // outside this sweeper's scope and must be left alone.
        using var directory = new TempDirectory();
        var policy = CreatePolicy(TimeSpan.Zero);
        var cleaner = new RotatedLogCleaner(policy, NullLogger<RotatedLogCleaner>.Instance);

        var foreignPath = Path.Combine(directory.Path, "worker-20260101T000000Z.log");
        File.WriteAllText(foreignPath, "{}\n");

        // Act
        cleaner.DeleteExpired(directory.Path, BaseName);

        // Assert
        Assert.True(File.Exists(foreignPath));
    }

    [Fact]
    public void DeleteExpired_should_ignore_engine_files_with_unparseable_timestamp()
    {
        // Arrange — `engine-not-a-timestamp.log` matches the
        // search pattern but its timestamp segment cannot be
        // parsed; the file must survive untouched.
        using var directory = new TempDirectory();
        var policy = CreatePolicy(TimeSpan.Zero);
        var cleaner = new RotatedLogCleaner(policy, NullLogger<RotatedLogCleaner>.Instance);

        var malformedPath = Path.Combine(directory.Path, "engine-not-a-timestamp.log");
        File.WriteAllText(malformedPath, "{}\n");

        // Act
        cleaner.DeleteExpired(directory.Path, BaseName);

        // Assert
        Assert.True(File.Exists(malformedPath));
    }

    [Fact]
    public void DeleteExpired_should_no_op_when_directory_is_missing()
    {
        // Arrange — point at a directory that does not exist.
        var policy = CreatePolicy(TimeSpan.Zero);
        var cleaner = new RotatedLogCleaner(policy, NullLogger<RotatedLogCleaner>.Instance);
        var missingDirectory = Path.Combine(
            Path.GetTempPath(),
            $"autocontext-cleaner-missing-{Guid.NewGuid():N}");

        // Act / Assert — must not throw.
        cleaner.DeleteExpired(missingDirectory, BaseName);
    }

    [Fact]
    public void DeleteExpired_should_throw_when_base_name_is_null_or_empty()
    {
        var cleaner = new RotatedLogCleaner(
            CreatePolicy(TimeSpan.Zero),
            NullLogger<RotatedLogCleaner>.Instance);

        Assert.Multiple(
            () => Assert.Throws<ArgumentNullException>(() => cleaner.DeleteExpired("anywhere", baseName: null!)),
            () => Assert.Throws<ArgumentException>(() => cleaner.DeleteExpired("anywhere", baseName: "")));
    }

    private static RetentionPolicy CreatePolicy(TimeSpan window) =>
        new(
            Options.Create(new EngineOptions { Retention = window }),
            new FakeTimeProvider(KnownNow));

    private static string SeedRotatedFile(string directory, DateTimeOffset stamp)
    {
        var path = Path.Combine(directory, RotatedLogCleaner.ComposeRotatedFileName(BaseName, stamp));
        File.WriteAllText(path, "{}\n");
        return path;
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"autocontext-cleaner-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch (IOException)
            {
                // Best-effort cleanup; ignore residual handles.
            }
        }
    }
}
