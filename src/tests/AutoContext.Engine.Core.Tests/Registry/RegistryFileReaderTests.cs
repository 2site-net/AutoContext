namespace AutoContext.Engine.Core.Tests.Registry;

using System.IO;
using System.Text;

using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Core.Tests.Testing.Utils;

/// <summary>
/// Tests for <see cref="RegistryFileReader"/> in isolation. The
/// reader is exercised against files seeded directly through
/// <see cref="File.WriteAllBytes(string, byte[])"/> or via the
/// production <see cref="RegistryFileWriter"/>; no service is
/// involved.
/// </summary>
public sealed class RegistryFileReaderTests : IDisposable
{
    private readonly string _directory;
    private readonly string _path;

    public RegistryFileReaderTests()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            $"ac-registry-reader-tests-{Guid.NewGuid():N}");
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
            () => Assert.Throws<ArgumentNullException>(() => new RegistryFileReader(null!)),
            () => Assert.Throws<ArgumentException>(() => new RegistryFileReader(string.Empty)),
            () => Assert.Throws<ArgumentException>(() => new RegistryFileReader("   ")));
    }

    [Fact]
    public async Task ReadAsync_should_return_empty_when_file_does_not_exist()
    {
        var sut = CreateReader();

        var entries = await sut.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Empty(entries);
    }

    [Fact]
    public async Task ReadAsync_should_return_entries_persisted_by_the_writer()
    {
        var writer = new RegistryFileWriter(_path);
        var seeded = new[]
        {
            RegistryEntryFakeData.CreateValidEntry(),
            RegistryEntryFakeData.CreateValidEntry(),
        };
        writer.Write(seeded);
        var sut = CreateReader();

        var entries = await sut.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(seeded.Length, entries.Count);
        Assert.Equal(seeded[0].InstanceId, entries[0].InstanceId);
        Assert.Equal(seeded[1].InstanceId, entries[1].InstanceId);
    }

    [Fact]
    public async Task ReadAsync_should_treat_corrupt_file_as_empty()
    {
        await File.WriteAllTextAsync(_path, "this is not json at all", TestContext.Current.CancellationToken);
        var sut = CreateReader();

        var entries = await sut.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Empty(entries);
    }

    [Fact]
    public async Task ReadAsync_should_treat_unknown_schema_version_as_empty()
    {
        var payload = Encoding.UTF8.GetBytes(
            """{"schemaVersion":99,"entries":[]}""");
        await File.WriteAllBytesAsync(_path, payload, TestContext.Current.CancellationToken);
        var sut = CreateReader();

        var entries = await sut.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Empty(entries);
    }

    private RegistryFileReader CreateReader() =>
        new(_path, new RegistryFileReaderOptions
        {
            InitialRetryDelay = TimeSpan.FromMilliseconds(1),
            MaxRetryDelay = TimeSpan.FromMilliseconds(5),
            MaxAttempts = 5,
        });
}
