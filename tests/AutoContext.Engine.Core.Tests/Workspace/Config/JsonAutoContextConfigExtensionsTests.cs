namespace AutoContext.Engine.Core.Tests.Workspace.Config;

using AutoContext.Engine.Core.Workspace.Config;

public sealed class JsonAutoContextConfigExtensionsTests
{
    public sealed class ToDomain
    {
        [Fact]
        public void Should_return_empty_graph_for_empty_json()
        {
            // Act
            var config = JsonAutoContextConfig.Empty.ToDomain();

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
            var json = new JsonAutoContextConfig
            {
                Version = "1.2.3",
                Diagnostic = new JsonDiagnosticConfig(WarnOnMissingId: false),
            };

            // Act
            var config = json.ToDomain();

            // Assert
            Assert.Multiple(
                () => Assert.Equal("1.2.3", config.Version),
                () => Assert.False(config.Diagnostic?.WarnOnMissingId));
        }

        [Fact]
        public void Should_map_instruction_file_with_disabled_rules()
        {
            // Arrange
            var json = new JsonAutoContextConfig
            {
                Instructions = new Dictionary<string, JsonInstructionsFileConfigEntry>
                {
                    ["a.md"] = new()
                    {
                        Version = "1.0",
                        Enabled = false,
                        DisabledInstructions = ["x", "y"],
                    },
                },
            };

            // Act
            var config = json.ToDomain();

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
        public void Should_map_shorthand_disabled_tool()
        {
            // Arrange
            var json = new JsonAutoContextConfig
            {
                McpTools = new Dictionary<string, JsonMcpToolConfigValue>
                {
                    ["t1"] = JsonMcpToolConfigValue.Disabled,
                },
            };

            // Act
            var config = json.ToDomain();

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
            var json = new JsonAutoContextConfig
            {
                McpTools = new Dictionary<string, JsonMcpToolConfigValue>
                {
                    ["t2"] = JsonMcpToolConfigValue.FromEntry(
                        new JsonMcpToolConfigEntry { Version = "2.0", DisabledTasks = ["k"] }),
                },
            };

            // Act
            var config = json.ToDomain();

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
