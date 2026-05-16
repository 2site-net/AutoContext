namespace AutoContext.Engine.Core.Tests.Registry;

using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Core.Tests.Testing.Utils;

public sealed class RegistryFileTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _path;

    public RegistryFileTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ac-engine-registry-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _path = Path.Combine(_tempDir, "engine-registry.json");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private RegistryFileReader CreateReader(RegistryFileOptions? options = null) =>
        new(_path, options);

    private RegistryFileWriter CreateWriter(RegistryFileOptions? options = null) =>
        new(_path, options);

    [Fact]
    public void Should_throw_on_null_or_whitespace_path()
    {
        // Act + Assert
        Assert.Multiple(
            () => Assert.Throws<ArgumentNullException>(() => new RegistryFileWriter(null!)),
            () => Assert.Throws<ArgumentException>(() => new RegistryFileWriter(string.Empty)),
            () => Assert.Throws<ArgumentException>(() => new RegistryFileWriter("   ")),
            () => Assert.Throws<ArgumentNullException>(() => new RegistryFileReader(null!)),
            () => Assert.Throws<ArgumentException>(() => new RegistryFileReader(string.Empty)),
            () => Assert.Throws<ArgumentException>(() => new RegistryFileReader("   ")));
    }

    [Fact]
    public async Task Should_read_empty_when_file_does_not_exist()
    {
        // Arrange
        var reader = CreateReader();

        // Act
        var entries = await reader.ReadAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(entries);
    }

    [Fact]
    public async Task Should_create_file_and_persist_entries_on_first_mutate()
    {
        // Arrange
        using var writer = CreateWriter();
        var reader = CreateReader();
        var entry = RegistryEntryFakeData.CreateValidEntry();

        // Act
        await writer.WriteAsync(_ => new[] { entry }, TestContext.Current.CancellationToken);
        var roundTrip = await reader.ReadAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.True(File.Exists(_path)),
            () => Assert.Single(roundTrip),
            () => Assert.Equal(entry, roundTrip[0]));
    }

    [Fact]
    public async Task Should_pass_current_entries_to_transform_for_append_then_remove()
    {
        // Arrange
        using var writer = CreateWriter();
        var reader = CreateReader();
        var first = RegistryEntryFakeData.CreateValidEntry();
        var second = RegistryEntryFakeData.CreateValidEntry();

        // Act — append two
        await writer.WriteAsync(current => current.Append(first).ToArray(), TestContext.Current.CancellationToken);
        await writer.WriteAsync(current => current.Append(second).ToArray(), TestContext.Current.CancellationToken);
        var afterAppend = await reader.ReadAsync(TestContext.Current.CancellationToken);

        // Act — remove the first
        await writer.WriteAsync(
            current => current.Where(e => e.InstanceId != first.InstanceId).ToArray(),
            TestContext.Current.CancellationToken);
        var afterRemove = await reader.ReadAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(2, afterAppend.Count),
            () => Assert.Contains(afterAppend, e => e.InstanceId == first.InstanceId),
            () => Assert.Contains(afterAppend, e => e.InstanceId == second.InstanceId),
            () => Assert.Single(afterRemove),
            () => Assert.Equal(second.InstanceId, afterRemove[0].InstanceId));
    }

    [Fact]
    public async Task Should_serialise_concurrent_in_process_mutates_without_losing_entries()
    {
        // Arrange
        using var writer = CreateWriter();
        var reader = CreateReader();
        var entries = Enumerable.Range(0, 10).Select(_ => RegistryEntryFakeData.CreateValidEntry()).ToArray();

        // Act — fan out 10 concurrent appends through the same writer instance
        var tasks = entries.Select(entry =>
            writer.WriteAsync(
                current => current.Append(entry).ToArray(),
                TestContext.Current.CancellationToken));
        await Task.WhenAll(tasks);

        // Assert
        var stored = await reader.ReadAsync(TestContext.Current.CancellationToken);
        var storedIds = stored.Select(e => e.InstanceId).ToHashSet();
        Assert.Multiple(
            () => Assert.Equal(10, stored.Count),
            () => Assert.All(entries, e => Assert.Contains(e.InstanceId, storedIds)));
    }

    [Fact]
    public async Task Should_recover_from_corrupt_file_by_truncate_and_reseed()
    {
        // Arrange — write garbage that is not valid JSON
        await File.WriteAllTextAsync(
            _path,
            "{ this is not json at all",
            TestContext.Current.CancellationToken);
        using var writer = CreateWriter();
        var reader = CreateReader();
        var entry = RegistryEntryFakeData.CreateValidEntry();

        // Act — mutate; previous corrupt content must be replaced by a fresh seed
        IReadOnlyList<RegistryEntry>? observed = null;
        await writer.WriteAsync(current =>
        {
            observed = current;
            return new[] { entry };
        }, TestContext.Current.CancellationToken);

        // Assert — corruption surfaced as an empty list to the transform,
        // and the file is now well-formed and contains only the new entry
        var roundTrip = await reader.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Multiple(
            () => Assert.NotNull(observed),
            () => Assert.Empty(observed!),
            () => Assert.Single(roundTrip),
            () => Assert.Equal(entry, roundTrip[0]));
    }

    [Fact]
    public async Task Should_treat_unknown_schema_version_as_empty_and_reseed()
    {
        // Arrange — well-formed JSON with a future schema version
        await File.WriteAllTextAsync(
            _path,
            """{ "schemaVersion": 999, "entries": [] }""",
            TestContext.Current.CancellationToken);
        using var writer = CreateWriter();
        var reader = CreateReader();
        var entry = RegistryEntryFakeData.CreateValidEntry();

        // Act
        IReadOnlyList<RegistryEntry>? observed = null;
        await writer.WriteAsync(current =>
        {
            observed = current;
            return new[] { entry };
        }, TestContext.Current.CancellationToken);
        var roundTrip = await reader.ReadAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.NotNull(observed),
            () => Assert.Empty(observed!),
            () => Assert.Single(roundTrip),
            () => Assert.Equal(entry, roundTrip[0]));
    }

    [Fact]
    public async Task Should_write_envelope_with_current_schema_version()
    {
        // Arrange
        using var writer = CreateWriter();

        // Act
        await writer.WriteAsync(_ => new[] { RegistryEntryFakeData.CreateValidEntry() }, TestContext.Current.CancellationToken);
        var raw = await File.ReadAllTextAsync(_path, TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Contains("\"schemaVersion\": 1", raw, StringComparison.Ordinal),
            () => Assert.Contains("\"entries\":", raw, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_throw_when_exclusive_open_retry_loop_exhausts()
    {
        // Arrange — hold the file open with FileShare.None so the writer cannot acquire it.
        // Use a tight retry budget so the test stays fast.
        using var blocker = new FileStream(
            _path,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None);
        using var writer = CreateWriter(new RegistryFileOptions
        {
            InitialRetryDelay = TimeSpan.FromMilliseconds(1),
            MaxRetryDelay = TimeSpan.FromMilliseconds(2),
            MaxAttempts = 3,
        });

        // Act + Assert
        await Assert.ThrowsAsync<IOException>(() =>
            writer.WriteAsync(_ => [], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Should_round_trip_all_entry_fields_with_correct_value_semantics()
    {
        // Arrange
        using var writer = CreateWriter();
        var reader = CreateReader();
        var entry = new RegistryEntry(
            WorkspaceHash: "abcdef0123456789",
            WorkspacePath: @"C:\workspaces\demo",
            InstanceId: Guid.NewGuid(),
            InstanceLabel: "vscode-window-1",
            ProcessId: 4242,
            ProcessStartTimeUtc: new DateTimeOffset(2026, 5, 14, 12, 34, 56, TimeSpan.Zero),
            EngineVersion: "0.9.5",
            StartedAt: new DateTimeOffset(2026, 5, 14, 12, 35, 0, TimeSpan.Zero),
            Retention: TimeSpan.FromDays(1));

        // Act
        await writer.WriteAsync(_ => new[] { entry }, TestContext.Current.CancellationToken);
        var roundTrip = await reader.ReadAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(entry, roundTrip[0]);
    }
}
