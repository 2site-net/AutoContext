namespace AutoContext.McpTools.Manifest.Generator.Tests;

using AutoContext.McpTools.Manifest.Generator;
using AutoContext.McpTools.Manifest.Generator.Tests.Support;

public sealed class McpToolsRegistryProjectorTests
{
    public sealed class Project
    {
        private readonly McpToolsRegistryProjector _sut = new();

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Should_reject_null_or_empty_registry_path(string? registryPath)
        {
            // Act + Assert
            Assert.ThrowsAny<ArgumentException>(() => _sut.Project(registryPath!));
        }

        [Fact]
        public void Should_throw_when_registry_missing()
        {
            // Arrange
            var missing = Path.Combine(Path.GetTempPath(), "ac-mcptools-gen-missing-" + Guid.NewGuid().ToString("N") + ".json");

            // Act + Assert
            Assert.Throws<InvalidOperationException>(() => _sut.Project(missing));
        }

        [Fact]
        public void Should_throw_on_unparsable_registry()
        {
            // Arrange
            using var file = new McpToolsRegistryFile("{ not json");

            // Act + Assert
            Assert.Throws<InvalidOperationException>(() => _sut.Project(file.RegistryPath));
        }

        [Fact]
        public void Should_throw_on_empty_registry()
        {
            // Arrange
            using var file = new McpToolsRegistryFile("null");

            // Act + Assert
            Assert.Throws<InvalidOperationException>(() => _sut.Project(file.RegistryPath));
        }

        [Fact]
        public void Should_project_empty_when_no_workers()
        {
            // Arrange
            using var file = new McpToolsRegistryFile(
                """
                {
                  "schemaVersion": "1",
                  "workers": []
                }
                """);

            // Act
            var catalog = _sut.Project(file.RegistryPath);

            // Assert
            Assert.Multiple(
                () => Assert.Equal("1", catalog.SchemaVersion),
                () => Assert.Empty(catalog.Tools));
        }

        [Fact]
        public void Should_flatten_workers_preserving_order_and_dropping_dispatch_fields()
        {
            // Arrange
            using var file = new McpToolsRegistryFile(
                """
                {
                  "schemaVersion": "1",
                  "workers": [
                    {
                      "id": "dotnet",
                      "name": "AutoContext.Worker.DotNet",
                      "tools": [
                        {
                          "name": "analyze_csharp_code",
                          "description": "Checks C#.",
                          "parameters": { "content": { "type": "string", "required": true } },
                          "tasks": [ { "name": "analyze_csharp_coding_style", "editorconfig": ["x"] } ]
                        }
                      ]
                    },
                    {
                      "id": "web",
                      "name": "AutoContext.Worker.Web",
                      "tools": [
                        {
                          "name": "analyze_typescript_code",
                          "description": "Checks TS.",
                          "tasks": [ { "name": "analyze_typescript_coding_style" } ]
                        }
                      ]
                    }
                  ]
                }
                """);

            // Act
            var catalog = _sut.Project(file.RegistryPath);

            // Assert
            Assert.Multiple(
                () => Assert.Equal("1", catalog.SchemaVersion),
                () => Assert.Equal(["analyze_csharp_code", "analyze_typescript_code"], catalog.Tools.Select(static tool => tool.Name)),
                () => Assert.Equal("Checks C#.", catalog.Tools[0].Description),
                () => Assert.Equal(["analyze_csharp_coding_style"], catalog.Tools[0].Tasks.Select(static task => task.Name)),
                () => Assert.Equal(["analyze_typescript_coding_style"], catalog.Tools[1].Tasks.Select(static task => task.Name)));
        }

        [Fact]
        public void Should_throw_on_duplicate_tool_name()
        {
            // Arrange
            using var file = new McpToolsRegistryFile(
                """
                {
                  "schemaVersion": "1",
                  "workers": [
                    { "id": "a", "tools": [ { "name": "dupe", "description": "First." } ] },
                    { "id": "b", "tools": [ { "name": "dupe", "description": "Second." } ] }
                  ]
                }
                """);

            // Act + Assert
            Assert.Throws<InvalidOperationException>(() => _sut.Project(file.RegistryPath));
        }

        [Fact]
        public void Should_throw_when_tool_name_missing()
        {
            // Arrange
            using var file = new McpToolsRegistryFile(
                """
                {
                  "workers": [ { "id": "a", "tools": [ { "description": "No name." } ] } ]
                }
                """);

            // Act + Assert
            Assert.Throws<InvalidOperationException>(() => _sut.Project(file.RegistryPath));
        }

        [Fact]
        public void Should_throw_when_tool_description_missing()
        {
            // Arrange
            using var file = new McpToolsRegistryFile(
                """
                {
                  "workers": [ { "id": "a", "tools": [ { "name": "no_description" } ] } ]
                }
                """);

            // Act + Assert
            Assert.Throws<InvalidOperationException>(() => _sut.Project(file.RegistryPath));
        }

        [Fact]
        public void Should_throw_when_task_name_missing()
        {
            // Arrange
            using var file = new McpToolsRegistryFile(
                """
                {
                  "workers": [
                    { "id": "a", "tools": [ { "name": "tool", "description": "Has a nameless task.", "tasks": [ { } ] } ] }
                  ]
                }
                """);

            // Act + Assert
            Assert.Throws<InvalidOperationException>(() => _sut.Project(file.RegistryPath));
        }
    }
}
