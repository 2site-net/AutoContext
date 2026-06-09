namespace AutoContext.Engine.Core.Tests.Workspace.Config;

using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;

public sealed class ConfigSnapshotExtensionsTests
{
    public sealed class ToFileFormat
    {
        [Fact]
        public void Should_produce_empty_json_for_empty_graph()
        {
            // Act
            var json = ConfigSnapshot.Empty.ToFileFormat();

            // Assert
            Assert.True(json.IsEmpty);
        }

        [Fact]
        public void Should_carry_engine_instruction_directories()
        {
            // Arrange
            var config = ConfigSnapshot.Empty with
            {
                Engine = new ConfigEngineSettings { InstructionsOverridesRoots = [".github", ".copilot"] },
            };

            // Act
            var json = config.ToFileFormat();

            // Assert
            Assert.Equal([".github", ".copilot"], json.Engine?.InstructionsOverridesRoots);
        }

        [Fact]
        public void Should_drop_engine_without_directories()
        {
            // Arrange
            var config = ConfigSnapshot.Empty with
            {
                Engine = new ConfigEngineSettings { InstructionsOverridesRoots = [] },
            };

            // Act
            var json = config.ToFileFormat();

            // Assert
            Assert.Null(json.Engine);
        }

        [Fact]
        public void Should_drop_instruction_file_without_state()
        {            // Arrange
            var config = ConfigSnapshot.Empty with
            {
                Instructions = [new ConfigInstructionsFile { Name = "a.md", Version = "1.0" }],
            };

            // Act
            var json = config.ToFileFormat();

            // Assert
            Assert.Null(json.Instructions);
        }

        [Fact]
        public void Should_emit_disabled_instruction_file_with_rules()
        {
            // Arrange
            var config = ConfigSnapshot.Empty with
            {
                Instructions =
                [
                    new ConfigInstructionsFile
                    {
                        Name = "a.md",
                        Disabled = true,
                        Version = "1.0",
                        Rules = [new ConfigInstructionsFile.InstructionsRule { Id = "x", Disabled = true }],
                    },
                ],
            };

            // Act
            var json = config.ToFileFormat();

            // Assert
            var entry = Assert.Single(json.Instructions!);

            Assert.Multiple(
                () => Assert.Equal("a.md", entry.Key),
                () => Assert.True(entry.Value.Disabled),
                () => Assert.Equal("1.0", entry.Value.Version),
                () => Assert.Equal(["x"], entry.Value.DisabledRules));
        }

        [Fact]
        public void Should_emit_disabled_only_tool()
        {
            // Arrange
            var config = ConfigSnapshot.Empty with
            {
                McpTools = [new ConfigMcpTool { Name = "t1", Disabled = true }],
            };

            // Act
            var json = config.ToFileFormat();

            // Assert
            var entry = Assert.Single(json.McpTools!);

            Assert.Multiple(
                () => Assert.Equal("t1", entry.Key),
                () => Assert.True(entry.Value.Disabled),
                () => Assert.Null(entry.Value.Version),
                () => Assert.Null(entry.Value.DisabledTasks));
        }

        [Fact]
        public void Should_emit_object_for_tool_with_disabled_tasks()
        {
            // Arrange
            var config = ConfigSnapshot.Empty with
            {
                McpTools =
                [
                    new ConfigMcpTool
                    {
                        Name = "t2",
                        Version = "2.0",
                        Tasks = [new ConfigMcpTool.McpTask { Name = "k", Disabled = true }],
                    },
                ],
            };

            // Act
            var json = config.ToFileFormat();

            // Assert
            var entry = Assert.Single(json.McpTools!);

            Assert.Multiple(
                () => Assert.Equal("t2", entry.Key),
                () => Assert.Null(entry.Value.Disabled),
                () => Assert.Equal("2.0", entry.Value.Version),
                () => Assert.Equal(["k"], entry.Value.DisabledTasks));
        }

        [Fact]
        public void Should_drop_tool_without_state()
        {
            // Arrange
            var config = ConfigSnapshot.Empty with
            {
                McpTools =
                [
                    new ConfigMcpTool
                    {
                        Name = "t3",
                        Tasks = [new ConfigMcpTool.McpTask { Name = "k", Disabled = null }],
                    },
                ],
            };

            // Act
            var json = config.ToFileFormat();

            // Assert
            Assert.Null(json.McpTools);
        }
    }
}
