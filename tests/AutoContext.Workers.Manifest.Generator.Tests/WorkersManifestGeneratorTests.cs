namespace AutoContext.Workers.Manifest.Generator.Tests;

using AutoContext.Workers.Manifest.Generator;
using AutoContext.Workers.Manifest.Generator.Tests.Support;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class WorkersManifestGeneratorTests
{
    public sealed class RunAsync
    {
        private static readonly WorkersManifestGenerator Sut = new(
            new WorkerDescriptorScanner(),
            new WorkersManifestSerializer(),
            NullLogger<WorkersManifestGenerator>.Instance);

        [Fact]
        public async Task Should_reject_null_args()
        {
            // Act + Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => Sut.RunAsync(null!));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(3)]
        public async Task Should_return_usage_when_arg_count_wrong(int count)
        {
            // Arrange
            var args = Enumerable.Repeat("x", count).ToArray();

            // Act
            var exitCode = await Sut.RunAsync(args);

            // Assert
            Assert.Equal(2, exitCode);
        }

        [Fact]
        public async Task Should_write_manifest_and_return_zero()
        {
            // Arrange
            using var tree = new WorkerSourceTree()
                .AddWorker(
                    "AutoContext.Worker.DotNet",
                    WorkersManifestFakeData.Descriptor("dotnet", "executable", "${root}/AutoContext.Worker.DotNet"))
                .AddWorker(
                    "AutoContext.Worker.Web",
                    WorkersManifestFakeData.Descriptor("web", "script", "node ${root}/index.js"));
            var outputPath = Path.Combine(tree.Root, "out", "workers.json");

            // Act
            var exitCode = await Sut.RunAsync([tree.Root, outputPath]);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(0, exitCode),
                () => Assert.Equal(
                    """
                    {
                      "workers": [
                        {
                          "id": "dotnet",
                          "type": "executable",
                          "command": "${root}/AutoContext.Worker.DotNet"
                        },
                        {
                          "id": "web",
                          "type": "script",
                          "command": "node ${root}/index.js"
                        }
                      ]
                    }

                    """,
                    File.ReadAllText(outputPath)));
        }

        [Fact]
        public async Task Should_return_one_on_duplicate_id()
        {
            // Arrange
            using var tree = new WorkerSourceTree()
                .AddWorker(
                    "AutoContext.Worker.Foo",
                    WorkersManifestFakeData.Descriptor("dupe", "executable", "${root}/AutoContext.Worker.Foo"))
                .AddWorker(
                    "AutoContext.Worker.Bar",
                    WorkersManifestFakeData.Descriptor("dupe", "executable", "${root}/AutoContext.Worker.Bar"));
            var outputPath = Path.Combine(tree.Root, "workers.json");

            // Act
            var exitCode = await Sut.RunAsync([tree.Root, outputPath]);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(1, exitCode),
                () => Assert.False(File.Exists(outputPath)));
        }

        [Fact]
        public async Task Should_return_one_on_missing_descriptor()
        {
            // Arrange
            using var tree = new WorkerSourceTree()
                .AddWorkerWithoutDescriptor("AutoContext.Worker.DotNet");
            var outputPath = Path.Combine(tree.Root, "workers.json");

            // Act
            var exitCode = await Sut.RunAsync([tree.Root, outputPath]);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(1, exitCode),
                () => Assert.False(File.Exists(outputPath)));
        }
    }
}
