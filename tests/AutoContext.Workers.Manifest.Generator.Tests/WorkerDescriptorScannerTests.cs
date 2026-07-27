namespace AutoContext.Workers.Manifest.Generator.Tests;

using AutoContext.Workers.Manifest.Generator;
using AutoContext.Workers.Manifest.Generator.Tests.Support;

public sealed class WorkerDescriptorScannerTests
{
    public sealed class Scan
    {
        private readonly WorkerDescriptorScanner _sut = new();

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Should_reject_null_or_empty_source_directory(string? sourceDirectory)
        {
            // Act + Assert
            Assert.ThrowsAny<ArgumentException>(() => _sut.Scan(sourceDirectory!));
        }

        [Fact]
        public void Should_throw_when_source_directory_missing()
        {
            // Arrange
            var missing = Path.Combine(Path.GetTempPath(), "ac-workers-gen-missing-" + Guid.NewGuid().ToString("N"));

            // Act + Assert
            Assert.Throws<InvalidOperationException>(() => _sut.Scan(missing));
        }

        [Fact]
        public void Should_read_executable_descriptor_verbatim()
        {
            // Arrange
            using var tree = new WorkerSourceTree()
                .AddWorker(
                    "AutoContext.Worker.DotNet",
                    WorkersManifestFakeData.Descriptor("dotnet", "executable", "${root}/AutoContext.Worker.DotNet"));

            // Act
            var manifest = _sut.Scan(tree.Root);

            // Assert
            var worker = Assert.Single(manifest.Workers);
            Assert.Multiple(
                () => Assert.Equal("dotnet", worker.Id),
                () => Assert.Equal("executable", worker.Type),
                () => Assert.Null(worker.Label),
                () => Assert.Equal("${root}/AutoContext.Worker.DotNet", worker.Command));
        }

        [Fact]
        public void Should_read_script_descriptor()
        {
            // Arrange
            using var tree = new WorkerSourceTree()
                .AddWorker(
                    "AutoContext.Worker.Web",
                    WorkersManifestFakeData.Descriptor("web", "script", "node ${root}/index.js"));

            // Act
            var manifest = _sut.Scan(tree.Root);

            // Assert
            var worker = Assert.Single(manifest.Workers);
            Assert.Multiple(
                () => Assert.Equal("web", worker.Id),
                () => Assert.Equal("script", worker.Type),
                () => Assert.Equal("node ${root}/index.js", worker.Command));
        }

        [Fact]
        public void Should_preserve_optional_label()
        {
            // Arrange
            using var tree = new WorkerSourceTree()
                .AddWorker(
                    "AutoContext.Worker.DotNet",
                    WorkersManifestFakeData.Descriptor(
                        "dotnet", "executable", "${root}/AutoContext.Worker.DotNet", label: ".NET worker"));

            // Act
            var manifest = _sut.Scan(tree.Root);

            // Assert
            var worker = Assert.Single(manifest.Workers);
            Assert.Equal(".NET worker", worker.Label);
        }

        [Fact]
        public void Should_sort_workers_by_id()
        {
            // Arrange
            using var tree = new WorkerSourceTree()
                .AddWorker(
                    "AutoContext.Worker.Workspace",
                    WorkersManifestFakeData.Descriptor("workspace", "executable", "${root}/AutoContext.Worker.Workspace"))
                .AddWorker(
                    "AutoContext.Worker.DotNet",
                    WorkersManifestFakeData.Descriptor("dotnet", "executable", "${root}/AutoContext.Worker.DotNet"))
                .AddWorker(
                    "AutoContext.Worker.Web",
                    WorkersManifestFakeData.Descriptor("web", "script", "node ${root}/index.js"));

            // Act
            var manifest = _sut.Scan(tree.Root);

            // Assert
            Assert.Equal(["dotnet", "web", "workspace"], manifest.Workers.Select(static w => w.Id));
        }

        [Fact]
        public void Should_throw_when_descriptor_missing()
        {
            // Arrange
            using var tree = new WorkerSourceTree()
                .AddWorkerWithoutDescriptor("AutoContext.Worker.DotNet");

            // Act + Assert
            Assert.Throws<InvalidOperationException>(() => _sut.Scan(tree.Root));
        }

        [Fact]
        public void Should_discover_a_worker_that_does_not_follow_the_naming_convention()
        {
            // Arrange
            using var tree = new WorkerSourceTree()
                .AddWorker(
                    "Contoso.CustomWorker",
                    WorkersManifestFakeData.Descriptor("custom", "executable", "${root}/Contoso.CustomWorker"));

            // Act
            var manifest = _sut.Scan(tree.Root);

            // Assert
            var worker = Assert.Single(manifest.Workers);
            Assert.Equal("custom", worker.Id);
        }

        [Fact]
        public void Should_ignore_a_directory_carrying_no_descriptor()
        {
            // Arrange
            using var tree = new WorkerSourceTree()
                .AddWorker(
                    "AutoContext.Worker.DotNet",
                    WorkersManifestFakeData.Descriptor("dotnet", "executable", "${root}/AutoContext.Worker.DotNet"))
                .AddWorkerWithoutDescriptor("AutoContext.Engine");

            // Act
            var manifest = _sut.Scan(tree.Root);

            // Assert
            Assert.Equal(["dotnet"], manifest.Workers.Select(static w => w.Id));
        }

        [Fact]
        public void Should_throw_on_duplicate_id()
        {
            // Arrange
            using var tree = new WorkerSourceTree()
                .AddWorker(
                    "AutoContext.Worker.Foo",
                    WorkersManifestFakeData.Descriptor("dupe", "executable", "${root}/AutoContext.Worker.Foo"))
                .AddWorker(
                    "AutoContext.Worker.Bar",
                    WorkersManifestFakeData.Descriptor("dupe", "executable", "${root}/AutoContext.Worker.Bar"));

            // Act + Assert
            Assert.Throws<InvalidOperationException>(() => _sut.Scan(tree.Root));
        }

        [Fact]
        public void Should_throw_on_unknown_type()
        {
            // Arrange
            using var tree = new WorkerSourceTree()
                .AddWorker(
                    "AutoContext.Worker.DotNet",
                    WorkersManifestFakeData.Descriptor("dotnet", "binary", "${root}/AutoContext.Worker.DotNet"));

            // Act + Assert
            Assert.Throws<InvalidOperationException>(() => _sut.Scan(tree.Root));
        }

        [Fact]
        public void Should_throw_when_required_field_missing()
        {
            // Arrange
            using var tree = new WorkerSourceTree()
                .AddWorker(
                    "AutoContext.Worker.DotNet",
                    """
                    {
                      "id": "dotnet",
                      "type": "executable"
                    }
                    """);

            // Act + Assert
            Assert.Throws<InvalidOperationException>(() => _sut.Scan(tree.Root));
        }

        [Fact]
        public void Should_throw_on_unparsable_descriptor()
        {
            // Arrange
            using var tree = new WorkerSourceTree()
                .AddWorker("AutoContext.Worker.DotNet", "{ not json");

            // Act + Assert
            Assert.Throws<InvalidOperationException>(() => _sut.Scan(tree.Root));
        }
    }
}
