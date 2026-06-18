namespace AutoContext.Engine.Core.Tests.Workers;

using AutoContext.Engine.Core.Workers;
using AutoContext.Engine.Tests.Support.IO;

public sealed class WorkersManifestLoaderTests
{
    public sealed class Load(TempDirectoryFixture tempDirectory) : IClassFixture<TempDirectoryFixture>
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Should_reject_missing_resources_directory(string? resourcesDirectory)
        {
            // Act + Assert
            Assert.ThrowsAny<ArgumentException>(
                () => WorkersManifestLoader.Load(resourcesDirectory!));
        }

        [Fact]
        public void Should_throw_when_manifest_file_is_absent()
        {
            // Arrange
            var directory = tempDirectory.CreateDirectory();

            // Act + Assert
            Assert.Throws<FileNotFoundException>(
                () => WorkersManifestLoader.Load(directory));
        }

        [Fact]
        public void Should_throw_when_manifest_is_not_valid_json()
        {
            // Arrange
            var path = tempDirectory.CreatePath(WorkersManifestLoader.ManifestFileName);
            File.WriteAllText(path, "{ not valid json");

            // Act + Assert
            Assert.Throws<InvalidOperationException>(
                () => WorkersManifestLoader.Load(Path.GetDirectoryName(path)!));
        }

        [Fact]
        public void Should_parse_worker_rows_in_document_order()
        {
            // Arrange
            var path = tempDirectory.CreatePath(WorkersManifestLoader.ManifestFileName);
            File.WriteAllText(
                path,
                """
                {
                  "workers": [
                    { "id": "dotnet", "type": "executable", "command": "${root}/AutoContext.Worker.DotNet" },
                    { "id": "web", "type": "script", "label": "Web worker", "command": "node ${root}/index.js" }
                  ]
                }
                """);

            // Act
            var manifest = WorkersManifestLoader.Load(Path.GetDirectoryName(path)!);

            // Assert
            Assert.NotNull(manifest.Workers);
            Assert.Multiple(
                () => Assert.Equal(2, manifest.Workers!.Count),
                () => Assert.Equal("dotnet", manifest.Workers![0].Id),
                () => Assert.Equal("executable", manifest.Workers![0].Type),
                () => Assert.Null(manifest.Workers![0].Label),
                () => Assert.Equal("${root}/AutoContext.Worker.DotNet", manifest.Workers![0].Command),
                () => Assert.Equal("web", manifest.Workers![1].Id),
                () => Assert.Equal("Web worker", manifest.Workers![1].Label),
                () => Assert.Equal("node ${root}/index.js", manifest.Workers![1].Command));
        }
    }
}
