namespace AutoContext.Engine.Core.Tests.Features.Instructions;

using AutoContext.Engine.Core.Features.Instructions;
using AutoContext.Engine.Core.Features.Instructions.Snapshot;
using AutoContext.Engine.Tests.Support.IO;
using AutoContext.Engine.Tests.Support.Logging;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class InstructionsOverridesStalenessInspectorTests
{
    public sealed class Constructor
    {
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Should_reject_blank_bundled_directory(string bundledDirectory)
            => Assert.Throws<ArgumentException>(
                () => new InstructionsOverridesStalenessInspector(
                    bundledDirectory, NullLogger.Instance));

        [Fact]
        public void Should_reject_null_logger()
            => Assert.Throws<ArgumentNullException>(
                () => new InstructionsOverridesStalenessInspector("bundled", null!));
    }

    public sealed class Inspect(TempDirectoryFixture tempDirectory)
        : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public void Should_reject_null_overrides()
        {
            // Arrange
            var inspector = new InstructionsOverridesStalenessInspector(
                tempDirectory.CreateDirectory(), NullLogger.Instance);

            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => inspector.Inspect(null!));
        }

        [Fact]
        public void Should_warn_when_override_is_older_than_bundled()
        {
            // Arrange
            const string fileName = "docker.instructions.md";
            var bundledDirectory = tempDirectory.CreateDirectory();
            var overrideDirectory = tempDirectory.CreateDirectory();
            var bundledPath = WriteFile(bundledDirectory, fileName, DateTime.UtcNow);
            var overridePath = WriteFile(
                overrideDirectory, fileName, File.GetLastWriteTimeUtc(bundledPath).AddDays(-1));

            var recorder = new FakeRecordingLogger();
            var inspector = new InstructionsOverridesStalenessInspector(bundledDirectory, recorder);

            // Act
            inspector.Inspect(SnapshotOf(fileName, overridePath));

            // Assert
            var entry = Assert.Single(recorder.Entries);
            Assert.Multiple(
                () => Assert.Equal(LogLevel.Warning, entry.Level),
                () => Assert.Contains(fileName, entry.Message, StringComparison.Ordinal));
        }

        [Fact]
        public void Should_not_warn_when_override_is_newer_than_bundled()
        {
            // Arrange
            const string fileName = "python.instructions.md";
            var bundledDirectory = tempDirectory.CreateDirectory();
            var overrideDirectory = tempDirectory.CreateDirectory();
            var bundledPath = WriteFile(bundledDirectory, fileName, DateTime.UtcNow.AddDays(-1));
            var overridePath = WriteFile(
                overrideDirectory, fileName, File.GetLastWriteTimeUtc(bundledPath).AddDays(1));

            var recorder = new FakeRecordingLogger();
            var inspector = new InstructionsOverridesStalenessInspector(bundledDirectory, recorder);

            // Act
            inspector.Inspect(SnapshotOf(fileName, overridePath));

            // Assert
            Assert.Empty(recorder.Entries);
        }

        [Fact]
        public void Should_skip_overrides_without_a_bundled_counterpart()
        {
            // Arrange
            const string fileName = "workspace-only.instructions.md";
            var bundledDirectory = tempDirectory.CreateDirectory();
            var overrideDirectory = tempDirectory.CreateDirectory();
            var overridePath = WriteFile(
                overrideDirectory, fileName, DateTime.UtcNow.AddDays(-1));

            var recorder = new FakeRecordingLogger();
            var inspector = new InstructionsOverridesStalenessInspector(bundledDirectory, recorder);

            // Act
            inspector.Inspect(SnapshotOf(fileName, overridePath));

            // Assert
            Assert.Empty(recorder.Entries);
        }

        private static string WriteFile(string directory, string fileName, DateTime lastWriteTimeUtc)
        {
            var path = Path.Combine(directory, fileName);
            File.WriteAllText(path, string.Empty);
            File.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
            return path;
        }

        private static InstructionsOverridesSnapshot SnapshotOf(string fileName, string overridePath)
            => new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [fileName] = overridePath,
            });
    }
}
