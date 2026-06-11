namespace AutoContext.Engine.Core.Tests.Features.Instructions;

using AutoContext.Engine.Core.Features.Instructions;
using AutoContext.Engine.Core.Features.Instructions.Snapshot;
using AutoContext.Engine.Tests.Support.IO;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class InstructionsManifestServiceTests
{
    private static InstructionsManifestService Create(string resourcesDirectory)
        => new(resourcesDirectory, NullLogger<InstructionsManifestService>.Instance);

    public sealed class Constructor
    {
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Should_reject_blank_resources_directory(string directory)
            => Assert.Throws<ArgumentException>(() => Create(directory));

        [Fact]
        public void Should_reject_null_logger()
            => Assert.Throws<ArgumentNullException>(
                () => new InstructionsManifestService("dir", null!));
    }

    public sealed class Current(TempDirectoryFixture tempDirectory)
        : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public void Should_be_empty_before_start()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsManifestTestFiles.WriteValid(directory);

            // Act
            var service = Create(directory);

            // Assert
            Assert.Same(InstructionsManifestSnapshot.Empty, service.Current);
            Assert.Empty(service.Current.Files);
        }
    }

    public sealed class StartAsync(TempDirectoryFixture tempDirectory)
        : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public async Task Should_publish_loaded_snapshot()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsManifestTestFiles.WriteValid(directory);
            var service = Create(directory);

            // Act
            await service.StartAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Collection(
                service.Current.Files,
                first => Assert.Equal("autocontext", first.Key),
                second => Assert.Equal("docker", second.Key));
        }

        [Fact]
        public async Task Should_throw_when_side_cars_missing()
        {
            // Arrange — empty directory, no side-cars.
            var service = Create(tempDirectory.CreateDirectory());

            // Act + Assert
            await Assert.ThrowsAsync<FileNotFoundException>(
                () => service.StartAsync(TestContext.Current.CancellationToken));
        }
    }

    public sealed class StopAsync(TempDirectoryFixture tempDirectory)
        : IClassFixture<TempDirectoryFixture>
    {
        [Fact]
        public async Task Should_complete_without_clearing_snapshot()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();
            InstructionsManifestTestFiles.WriteValid(directory);
            var service = Create(directory);
            await service.StartAsync(TestContext.Current.CancellationToken);

            // Act
            await service.StopAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(2, service.Current.Files.Count);
        }
    }
}
