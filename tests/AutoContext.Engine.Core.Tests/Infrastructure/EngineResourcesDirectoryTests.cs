namespace AutoContext.Engine.Core.Tests.Infrastructure;

using AutoContext.Engine.Core.Infrastructure;
using AutoContext.Engine.Tests.Support.IO;

public sealed class EngineResourcesDirectoryTests
{
    public sealed class Constructor
    {
        [Fact]
        public void Should_reject_null_base_directory()
        {
            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => new EngineResourcesDirectory(null!));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Should_reject_empty_or_whitespace_base_directory(string baseDirectory)
        {
            // Act + Assert
            Assert.Throws<ArgumentException>(() => new EngineResourcesDirectory(baseDirectory));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Should_reject_empty_or_whitespace_override_directory(string overrideDirectory)
        {
            // Arrange
            var baseDirectory = OperatingSystem.IsWindows() ? @"C:\acx\base" : "/acx/base";

            // Act + Assert
            Assert.Throws<ArgumentException>(
                () => new EngineResourcesDirectory(baseDirectory, overrideDirectory));
        }

        [Fact]
        public void Should_accept_null_override_directory()
        {
            // Arrange
            var baseDirectory = OperatingSystem.IsWindows() ? @"C:\acx\base" : "/acx/base";

            // Act
            var resources = new EngineResourcesDirectory(baseDirectory);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(baseDirectory, resources.BaseDirectory),
                () => Assert.Null(resources.OverrideDirectory));
        }

        [Fact]
        public void Should_expose_both_roots_when_override_supplied()
        {
            // Arrange
            var baseDirectory = OperatingSystem.IsWindows() ? @"C:\acx\base" : "/acx/base";
            var overrideDirectory = OperatingSystem.IsWindows() ? @"C:\acx\override" : "/acx/override";

            // Act
            var resources = new EngineResourcesDirectory(baseDirectory, overrideDirectory);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(baseDirectory, resources.BaseDirectory),
                () => Assert.Equal(overrideDirectory, resources.OverrideDirectory));
        }
    }

    public sealed class ResolveFile(TempDirectoryFixture tempDirectory)
        : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public void Should_return_override_path_when_override_file_exists()
        {
            // Arrange
            var baseDirectory = tempDirectory.CreateDirectory();
            var overrideDirectory = tempDirectory.CreateDirectory();
            File.WriteAllText(Path.Combine(overrideDirectory, "workers.json"), "{}");
            var resources = new EngineResourcesDirectory(baseDirectory, overrideDirectory);

            // Act
            var resolved = resources.ResolveFile("workers.json");

            // Assert
            Assert.Equal(Path.Combine(overrideDirectory, "workers.json"), resolved);
        }

        [Fact]
        public void Should_fall_through_to_base_when_override_file_missing()
        {
            // Arrange
            var baseDirectory = tempDirectory.CreateDirectory();
            var overrideDirectory = tempDirectory.CreateDirectory();
            var resources = new EngineResourcesDirectory(baseDirectory, overrideDirectory);

            // Act
            var resolved = resources.ResolveFile("workers.json");

            // Assert
            Assert.Equal(Path.Combine(baseDirectory, "workers.json"), resolved);
        }

        [Fact]
        public void Should_return_base_path_when_no_override()
        {
            // Arrange
            var baseDirectory = tempDirectory.CreateDirectory();
            var resources = new EngineResourcesDirectory(baseDirectory);

            // Act
            var resolved = resources.ResolveFile("workers.json");

            // Assert
            Assert.Equal(Path.Combine(baseDirectory, "workers.json"), resolved);
        }

        [Fact]
        public void Should_reject_null_file_name()
        {
            // Arrange
            var resources = new EngineResourcesDirectory(tempDirectory.CreateDirectory());

            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => resources.ResolveFile(null!));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Should_reject_empty_or_whitespace_file_name(string fileName)
        {
            // Arrange
            var resources = new EngineResourcesDirectory(tempDirectory.CreateDirectory());

            // Act + Assert
            Assert.Throws<ArgumentException>(() => resources.ResolveFile(fileName));
        }
    }

    public sealed class SubDirectory(TempDirectoryFixture tempDirectory)
        : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public void Should_resolve_override_file_within_subdirectory()
        {
            // Arrange
            var baseDirectory = tempDirectory.CreateDirectory();
            var overrideDirectory = tempDirectory.CreateDirectory();
            var overrideSubdirectory = Path.Combine(overrideDirectory, "Instructions");
            Directory.CreateDirectory(overrideSubdirectory);
            File.WriteAllText(Path.Combine(overrideSubdirectory, "x.instructions.md"), "body");
            var resources = new EngineResourcesDirectory(baseDirectory, overrideDirectory)
                .SubDirectory("Instructions");

            // Act
            var resolved = resources.ResolveFile("x.instructions.md");

            // Assert
            Assert.Equal(Path.Combine(overrideSubdirectory, "x.instructions.md"), resolved);
        }

        [Fact]
        public void Should_fall_through_to_base_subdirectory_without_override()
        {
            // Arrange
            var baseDirectory = tempDirectory.CreateDirectory();
            var resources = new EngineResourcesDirectory(baseDirectory).SubDirectory("Instructions");

            // Act
            var resolved = resources.ResolveFile("x.instructions.md");

            // Assert
            Assert.Equal(
                Path.Combine(baseDirectory, "Instructions", "x.instructions.md"), resolved);
        }

        [Fact]
        public void Should_reject_null_name()
        {
            // Arrange
            var resources = new EngineResourcesDirectory(tempDirectory.CreateDirectory());

            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => resources.SubDirectory(null!));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Should_reject_empty_or_whitespace_name(string name)
        {
            // Arrange
            var resources = new EngineResourcesDirectory(tempDirectory.CreateDirectory());

            // Act + Assert
            Assert.Throws<ArgumentException>(() => resources.SubDirectory(name));
        }
    }

    public sealed class Conversion
    {
        [Fact]
        public void Should_widen_string_to_overlay_without_override()
        {
            // Arrange
            var baseDirectory = OperatingSystem.IsWindows() ? @"C:\acx\base" : "/acx/base";

            // Act
            EngineResourcesDirectory resources = baseDirectory;

            // Assert
            Assert.Multiple(
                () => Assert.Equal(baseDirectory, resources.BaseDirectory),
                () => Assert.Null(resources.OverrideDirectory));
        }

        [Fact]
        public void Should_create_overlay_without_override_from_directory()
        {
            // Arrange
            var baseDirectory = OperatingSystem.IsWindows() ? @"C:\acx\base" : "/acx/base";

            // Act
            var resources = EngineResourcesDirectory.FromDirectory(baseDirectory);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(baseDirectory, resources.BaseDirectory),
                () => Assert.Null(resources.OverrideDirectory));
        }
    }
}
