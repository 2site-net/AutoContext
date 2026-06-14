namespace AutoContext.McpTools.Manifest.Generator.Tests;

using AutoContext.McpTools.Manifest.Generator;
using AutoContext.McpTools.Manifest.Generator.Tests.Support;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class McpToolsManifestGeneratorTests
{
    public sealed class RunAsync
    {
        private static readonly McpToolsManifestGenerator Sut = new(
            new McpToolsRegistryProjector(),
            new McpToolsManifestSerializer(),
            NullLogger<McpToolsManifestGenerator>.Instance);

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
        public async Task Should_write_catalog_and_return_zero()
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
            var exitCode = await Sut.RunAsync([file.RegistryPath, file.OutputPath]);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(0, exitCode),
                () => Assert.Equal(
                    """
                    {
                      "schemaVersion": "1",
                      "tools": [
                        {
                          "name": "analyze_csharp_code",
                          "description": "Checks C#.",
                          "tasks": [
                            {
                              "name": "analyze_csharp_coding_style"
                            }
                          ]
                        },
                        {
                          "name": "analyze_typescript_code",
                          "description": "Checks TS.",
                          "tasks": [
                            {
                              "name": "analyze_typescript_coding_style"
                            }
                          ]
                        }
                      ]
                    }

                    """,
                    File.ReadAllText(file.OutputPath)));
        }

        [Fact]
        public async Task Should_return_one_on_duplicate_tool_name()
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

            // Act
            var exitCode = await Sut.RunAsync([file.RegistryPath, file.OutputPath]);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(1, exitCode),
                () => Assert.False(File.Exists(file.OutputPath)));
        }

        [Fact]
        public async Task Should_return_one_on_missing_registry()
        {
            // Arrange
            var missing = Path.Combine(Path.GetTempPath(), "ac-mcptools-gen-missing-" + Guid.NewGuid().ToString("N") + ".json");
            var outputPath = Path.Combine(Path.GetTempPath(), "ac-mcptools-gen-out-" + Guid.NewGuid().ToString("N") + ".json");

            // Act
            var exitCode = await Sut.RunAsync([missing, outputPath]);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(1, exitCode),
                () => Assert.False(File.Exists(outputPath)));
        }
    }
}
