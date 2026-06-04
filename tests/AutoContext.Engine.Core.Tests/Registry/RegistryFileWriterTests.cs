namespace AutoContext.Engine.Core.Tests.Registry;

using System.IO;
using System.Linq;
using System.Text.Json;

using AutoContext.Engine.Core.Registry;
using AutoContext.Engine.Core.Tests.Support.Registry;
using AutoContext.Engine.Protocol.Messages.Registry;
using AutoContext.Engine.Tests.Support.IO;

/// <summary>
/// Tests for the atomic, single-shot <see cref="RegistryFileWriter"/>.
/// The writer is intentionally narrow — temp+fsync+rename only —
/// so the suite focuses on atomicity, durability of the final
/// state, and cleanup on failure. Cross-process coordination,
/// retry, and the read-modify-write cycle live in
/// <see cref="RegistryFileService"/> and are exercised in
/// <see cref="RegistryFileServiceTests"/>.
/// </summary>
public sealed class RegistryFileWriterTests
{
    private const string RegistryFileName = "engine-registry.json";

    public sealed class Constructor
    {
        [Fact]
        public void Should_reject_null_or_whitespace_path()
        {
            Assert.Multiple(
                () => Assert.Throws<ArgumentNullException>(() => new RegistryFileWriter(null!)),
                () => Assert.Throws<ArgumentException>(() => new RegistryFileWriter(string.Empty)),
                () => Assert.Throws<ArgumentException>(() => new RegistryFileWriter("   ")));
        }
    }

    public sealed class Write(TempDirectoryFixture tempDirectory) : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public void Should_atomically_replace_existing_file_content()
        {
            var path = tempDirectory.CreatePath(RegistryFileName);
            var sut = new RegistryFileWriter(path);
            sut.Write([RegistryEntryFakeData.CreateValidEntry()]);

            sut.Write(
            [
                RegistryEntryFakeData.CreateValidEntry(),
                RegistryEntryFakeData.CreateValidEntry(),
            ]);

            // Assert against the parsed structure rather than raw bytes
            // so this does not silently rely on the fake-data factory
            // emitting different values across calls.
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            Assert.Multiple(
                () => Assert.Equal(2, document.RootElement.GetProperty("entries").GetArrayLength()),
                () => Assert.Single(
                    Directory.EnumerateFiles(Path.GetDirectoryName(path)!, RegistryFileName)));
        }

        [Fact]
        public void Should_create_the_file_when_it_does_not_exist()
        {
            var path = tempDirectory.CreatePath(RegistryFileName);
            var sut = new RegistryFileWriter(path);
            var entry = RegistryEntryFakeData.CreateValidEntry();

            sut.Write([entry]);

            Assert.True(File.Exists(path));
        }

        [Fact]
        public void Should_emit_envelope_with_current_schema_version()
        {
            var path = tempDirectory.CreatePath(RegistryFileName);
            var sut = new RegistryFileWriter(path);
            var entry = RegistryEntryFakeData.CreateValidEntry();

            sut.Write([entry]);

            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            Assert.Multiple(
                () => Assert.Equal(
                    RegistryFileFormat.CurrentSchemaVersion,
                    document.RootElement.GetProperty("schemaVersion").GetInt32()),
                () => Assert.Equal(1, document.RootElement.GetProperty("entries").GetArrayLength()));
        }

        [Fact]
        public void Should_not_leak_temp_files_on_success()
        {
            var path = tempDirectory.CreatePath(RegistryFileName);
            var sut = new RegistryFileWriter(path);

            sut.Write([RegistryEntryFakeData.CreateValidEntry()]);
            sut.Write([RegistryEntryFakeData.CreateValidEntry()]);
            sut.Write([RegistryEntryFakeData.CreateValidEntry()]);

            var temps = Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.tmp").ToArray();
            Assert.Empty(temps);
        }

        [Fact]
        public void Should_reject_null_entries()
        {
            var path = tempDirectory.CreatePath(RegistryFileName);
            var sut = new RegistryFileWriter(path);

            Assert.Throws<ArgumentNullException>(() => sut.Write(null!));
        }
    }
}
