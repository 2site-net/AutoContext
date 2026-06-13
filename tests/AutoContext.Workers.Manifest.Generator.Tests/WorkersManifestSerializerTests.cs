namespace AutoContext.Workers.Manifest.Generator.Tests;

using AutoContext.Workers.Manifest.Generator;
using AutoContext.Workers.Manifest.Generator.Tests.Support;

public sealed class WorkersManifestSerializerTests
{
    public sealed class Serialize
    {
        private readonly WorkersManifestSerializer _sut = new();

        [Fact]
        public void Should_reject_null_manifest()
        {
            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => _sut.Serialize(null!));
        }

        [Fact]
        public void Should_emit_empty_workers_array()
        {
            // Arrange
            var manifest = WorkersManifestFakeData.CreateManifest();

            // Act
            var json = _sut.Serialize(manifest);

            // Assert
            Assert.Equal(
                """
                {
                  "workers": []
                }

                """,
                json);
        }

        [Fact]
        public void Should_emit_worker_fields_in_canonical_order()
        {
            // Arrange
            var manifest = WorkersManifestFakeData.CreateManifest(WorkersManifestFakeData.CreateEntry());

            // Act
            var json = _sut.Serialize(manifest);

            // Assert
            Assert.Equal(
                """
                {
                  "workers": [
                    {
                      "id": "dotnet",
                      "type": "executable",
                      "command": "${root}/AutoContext.Worker.DotNet"
                    }
                  ]
                }

                """,
                json);
        }

        [Fact]
        public void Should_emit_label_when_present()
        {
            // Arrange
            var manifest = WorkersManifestFakeData.CreateManifest(
                WorkersManifestFakeData.CreateEntry(label: ".NET worker"));

            // Act
            var json = _sut.Serialize(manifest);

            // Assert
            Assert.Equal(
                """
                {
                  "workers": [
                    {
                      "id": "dotnet",
                      "type": "executable",
                      "label": ".NET worker",
                      "command": "${root}/AutoContext.Worker.DotNet"
                    }
                  ]
                }

                """,
                json);
        }

        [Fact]
        public void Should_omit_label_when_null()
        {
            // Arrange
            var manifest = WorkersManifestFakeData.CreateManifest(
                WorkersManifestFakeData.CreateEntry(label: null));

            // Act
            var json = _sut.Serialize(manifest);

            // Assert
            Assert.DoesNotContain("\"label\"", json, StringComparison.Ordinal);
        }
    }
}
