namespace AutoContext.McpTools.Manifest.Generator.Tests;

using AutoContext.McpTools.Manifest.Generator;
using AutoContext.McpTools.Manifest.Generator.Tests.Support;

public sealed class McpToolsManifestSerializerTests
{
    public sealed class Serialize
    {
        private readonly McpToolsManifestSerializer _sut = new();

        [Fact]
        public void Should_reject_null_catalog()
        {
            // Act + Assert
            Assert.Throws<ArgumentNullException>(() => _sut.Serialize(null!));
        }

        [Fact]
        public void Should_emit_empty_tools_array()
        {
            // Arrange
            var catalog = McpToolsManifestFakeData.CreateCatalog();

            // Act
            var json = _sut.Serialize(catalog);

            // Assert
            Assert.Equal(
                """
                {
                  "schemaVersion": "1",
                  "tools": []
                }

                """,
                json);
        }

        [Fact]
        public void Should_emit_tool_fields_in_canonical_order()
        {
            // Arrange
            var catalog = McpToolsManifestFakeData.CreateCatalog(
                McpToolsManifestFakeData.CreateEntry("analyze_csharp_code", "Checks C#.", "task_a", "task_b"));

            // Act
            var json = _sut.Serialize(catalog);

            // Assert
            Assert.Equal(
                """
                {
                  "schemaVersion": "1",
                  "tools": [
                    {
                      "name": "analyze_csharp_code",
                      "description": "Checks C#.",
                      "tasks": [
                        {
                          "name": "task_a"
                        },
                        {
                          "name": "task_b"
                        }
                      ]
                    }
                  ]
                }

                """,
                json);
        }

        [Fact]
        public void Should_emit_empty_tasks_array_for_taskless_tool()
        {
            // Arrange
            var catalog = McpToolsManifestFakeData.CreateCatalog(
                McpToolsManifestFakeData.CreateEntry("read_editorconfig", "Resolves rules."));

            // Act
            var json = _sut.Serialize(catalog);

            // Assert
            Assert.Equal(
                """
                {
                  "schemaVersion": "1",
                  "tools": [
                    {
                      "name": "read_editorconfig",
                      "description": "Resolves rules.",
                      "tasks": []
                    }
                  ]
                }

                """,
                json);
        }
    }
}
