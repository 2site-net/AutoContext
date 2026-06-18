namespace AutoContext.Engine.Core.Tests.Features.McpTools;

using AutoContext.Engine.Core.Features.McpTools;
using AutoContext.Engine.Core.Tests.Support.Features.McpTools;

public sealed class McpToolsCatalogSchemaValidatorTests
{
    public sealed class Validate
    {
        [Fact]
        public void Should_accept_a_valid_catalog()
        {
            // Act
            var result = McpToolsCatalogSchemaValidator.Validate(
                McpToolsRegistryTestFiles.CatalogJson,
                McpToolsRegistryTestFiles.CatalogSchemaJson);

            // Assert
            Assert.Multiple(
                () => Assert.True(result.IsValid),
                () => Assert.Empty(result.Errors));
        }

        [Fact]
        public void Should_report_a_schema_violation()
        {
            // Arrange - the tool name violates the snake_case pattern.
            var catalog =
                """
                {
                  "schemaVersion": "1",
                  "categories": [
                    {
                      "name": "Workspace",
                      "description": "Workspace-level tools.",
                      "workerId": "workspace"
                    }
                  ],
                  "tools": [
                    {
                      "name": "AnalyzeGitCommit",
                      "description": "Validate commit messages.",
                      "category": "Workspace"
                    }
                  ]
                }
                """;

            // Act
            var result = McpToolsCatalogSchemaValidator.Validate(
                catalog, McpToolsRegistryTestFiles.CatalogSchemaJson);

            // Assert
            Assert.Multiple(
                () => Assert.False(result.IsValid),
                () => Assert.NotEmpty(result.Errors));
        }

        [Fact]
        public void Should_report_a_missing_required_field()
        {
            // Arrange - the category omits its description.
            var catalog =
                """
                {
                  "schemaVersion": "1",
                  "categories": [
                    {
                      "name": "Workspace",
                      "workerId": "workspace"
                    }
                  ],
                  "tools": [
                    {
                      "name": "analyze_git_commit_message",
                      "description": "Validate commit messages.",
                      "category": "Workspace"
                    }
                  ]
                }
                """;

            // Act
            var result = McpToolsCatalogSchemaValidator.Validate(
                catalog, McpToolsRegistryTestFiles.CatalogSchemaJson);

            // Assert
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Should_report_malformed_catalog_json()
        {
            // Act
            var result = McpToolsCatalogSchemaValidator.Validate(
                "not json", McpToolsRegistryTestFiles.CatalogSchemaJson);

            // Assert
            Assert.Multiple(
                () => Assert.False(result.IsValid),
                () => Assert.Contains(
                    result.Errors,
                    error => error.Contains("not valid JSON", StringComparison.Ordinal)));
        }

        [Fact]
        public void Should_reject_null_arguments()
        {
            // Act + Assert
            Assert.Multiple(
                () => Assert.Throws<ArgumentNullException>(
                    () => McpToolsCatalogSchemaValidator.Validate(
                        null!, McpToolsRegistryTestFiles.CatalogSchemaJson)),
                () => Assert.Throws<ArgumentNullException>(
                    () => McpToolsCatalogSchemaValidator.Validate(
                        McpToolsRegistryTestFiles.CatalogJson, null!)));
        }
    }
}
