namespace AutoContext.Engine.Core.Tests.Workspace.Config;

using AutoContext.Engine.Core.Workspace.Config;

public sealed class AutoContextConfigExtensionsTests
{
    public sealed class ToJson
    {
        [Fact]
        public void Should_produce_empty_json_for_empty_graph()
        {
            // Act
            var json = AutoContextConfig.Empty.ToJson();

            // Assert
            Assert.True(json.IsEmpty);
        }

        [Fact]
        public void Should_drop_instruction_file_without_state()
        {
            // Arrange
            var config = AutoContextConfig.Empty with
            {
                Instructions = [new InstructionsFileConfig { Name = "a.md", Version = "1.0" }],
            };

            // Act
            var json = config.ToJson();

            // Assert
            Assert.Null(json.Instructions);
        }

        [Fact]
        public void Should_emit_disabled_instruction_file_with_rules()
        {
            // Arrange
            var config = AutoContextConfig.Empty with
            {
                Instructions =
                [
                    new InstructionsFileConfig
                    {
                        Name = "a.md",
                        Disabled = true,
                        Version = "1.0",
                        Rules = [new InstructionsFileConfig.InstructionsRule { Id = "x", Disabled = true }],
                    },
                ],
            };

            // Act
            var json = config.ToJson();

            // Assert
            var entry = Assert.Single(json.Instructions!);

            Assert.Multiple(
                () => Assert.Equal("a.md", entry.Key),
                () => Assert.False(entry.Value.Enabled),
                () => Assert.Equal("1.0", entry.Value.Version),
                () => Assert.Equal(["x"], entry.Value.DisabledInstructions));
        }

        [Fact]
        public void Should_emit_shorthand_for_disabled_only_tool()
        {
            // Arrange
            var config = AutoContextConfig.Empty with
            {
                McpTools = [new McpToolConfig { Name = "t1", Disabled = true }],
            };

            // Act
            var json = config.ToJson();

            // Assert
            var entry = Assert.Single(json.McpTools!);

            Assert.Multiple(
                () => Assert.Equal("t1", entry.Key),
                () => Assert.True(entry.Value.IsShorthandDisabled));
        }

        [Fact]
        public void Should_emit_object_for_tool_with_disabled_tasks()
        {
            // Arrange
            var config = AutoContextConfig.Empty with
            {
                McpTools =
                [
                    new McpToolConfig
                    {
                        Name = "t2",
                        Version = "2.0",
                        Tasks = [new McpToolConfig.McpTask { Name = "k", Disabled = true }],
                    },
                ],
            };

            // Act
            var json = config.ToJson();

            // Assert
            var entry = Assert.Single(json.McpTools!);

            Assert.Multiple(
                () => Assert.Equal("t2", entry.Key),
                () => Assert.False(entry.Value.IsShorthandDisabled),
                () => Assert.Equal("2.0", entry.Value.Entry?.Version),
                () => Assert.Equal(["k"], entry.Value.Entry?.DisabledTasks));
        }

        [Fact]
        public void Should_drop_tool_without_state()
        {
            // Arrange
            var config = AutoContextConfig.Empty with
            {
                McpTools =
                [
                    new McpToolConfig
                    {
                        Name = "t3",
                        Tasks = [new McpToolConfig.McpTask { Name = "k", Disabled = null }],
                    },
                ],
            };

            // Act
            var json = config.ToJson();

            // Assert
            Assert.Null(json.McpTools);
        }
    }
}
