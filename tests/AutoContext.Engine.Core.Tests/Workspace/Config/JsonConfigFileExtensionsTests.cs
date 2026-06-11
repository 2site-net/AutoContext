namespace AutoContext.Engine.Core.Tests.Workspace.Config;

using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Config.Format;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;

public sealed class JsonConfigFileExtensionsTests
{
    public sealed class ToDomainGraph
    {
        [Fact]
        public void Should_return_empty_graph_for_empty_json()
        {
            // Act
            var config = JsonConfigFile.Empty.ToDomainGraph();

            // Assert
            Assert.Multiple(
                () => Assert.Null(config.Version),
                () => Assert.Null(config.Diagnostic),
                () => Assert.Empty(config.Instructions),
                () => Assert.Empty(config.McpTools));
        }

        [Fact]
        public void Should_map_version_and_diagnostic()
        {
            // Arrange
            var json = new JsonConfigFile
            {
                Version = "1.2.3",
                Diagnostic = new JsonConfigFileDiagnostic(WarnOnMissingId: false),
            };

            // Act
            var config = json.ToDomainGraph();

            // Assert
            Assert.Multiple(
                () => Assert.Equal("1.2.3", config.Version),
                () => Assert.False(config.Diagnostic?.WarnOnMissingId));
        }

        [Fact]
        public void Should_map_engine_instruction_directories()
        {
            // Arrange
            var json = new JsonConfigFile
            {
                Engine = new JsonConfigFileEngine([".github", ".copilot"]),
            };

            // Act
            var config = json.ToDomainGraph();

            // Assert
            Assert.Equal([".github", ".copilot"], config.Engine?.InstructionsOverridesRoots);
        }

        [Fact]
        public void Should_map_instruction_file_with_disabled_rules()
        {
            // Arrange
            var json = new JsonConfigFile
            {
                Instructions = new Dictionary<string, JsonConfigFileInstructionsEntry>
                {
                    ["a.md"] = new()
                    {
                        Version = "1.0",
                        Disabled = true,
                        DisabledRules = ["x", "y"],
                    },
                },
            };

            // Act
            var config = json.ToDomainGraph();

            // Assert
            var file = Assert.Single(config.Instructions);

            Assert.Multiple(
                () => Assert.Equal("a.md", file.Name),
                () => Assert.True(file.Disabled),
                () => Assert.Equal("1.0", file.Version),
                () => Assert.Equal(["x", "y"], file.Rules.Select(rule => rule.Id)),
                () => Assert.All(file.Rules, rule => Assert.True(rule.Disabled)));
        }

        [Fact]
        public void Should_map_disabled_tool()
        {
            // Arrange
            var json = new JsonConfigFile
            {
                McpTools = new Dictionary<string, JsonConfigFileMcpToolEntry>
                {
                    ["t1"] = new() { Disabled = true },
                },
            };

            // Act
            var config = json.ToDomainGraph();

            // Assert
            var tool = Assert.Single(config.McpTools);

            Assert.Multiple(
                () => Assert.Equal("t1", tool.Name),
                () => Assert.True(tool.Disabled),
                () => Assert.Null(tool.Version),
                () => Assert.Empty(tool.Tasks));
        }

        [Fact]
        public void Should_map_object_tool_with_disabled_tasks()
        {
            // Arrange
            var json = new JsonConfigFile
            {
                McpTools = new Dictionary<string, JsonConfigFileMcpToolEntry>
                {
                    ["t2"] = new() { Version = "2.0", DisabledTasks = ["k"] },
                },
            };

            // Act
            var config = json.ToDomainGraph();

            // Assert
            var tool = Assert.Single(config.McpTools);

            Assert.Multiple(
                () => Assert.Equal("t2", tool.Name),
                () => Assert.Null(tool.Disabled),
                () => Assert.Equal("2.0", tool.Version),
                () => Assert.Equal("k", Assert.Single(tool.Tasks).Name),
                () => Assert.True(Assert.Single(tool.Tasks).Disabled));
        }
    }
}
