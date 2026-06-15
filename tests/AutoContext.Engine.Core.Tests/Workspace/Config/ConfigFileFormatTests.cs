namespace AutoContext.Engine.Core.Tests.Workspace.Config;

using System.Text;

using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Config.Format;

public sealed class ConfigFileFormatTests
{
    public sealed class Serialize
    {
        [Fact]
        public void Should_write_canonical_camelcase_json_with_trailing_newline()
        {
            // Arrange
            var config = new JsonConfigFile
            {
                Instructions = new Dictionary<string, JsonConfigFileInstructionsEntry>
                {
                    ["a.md"] = new()
                    {
                        Version = "1.0",
                        Disabled = true,
                        DisabledRules = ["x"],
                    },
                },
                McpTools = new Dictionary<string, JsonConfigFileMcpToolEntry>
                {
                    ["t1"] = new() { Disabled = true },
                    ["t2"] = new() { Disabled = true },
                },
            };

            var expected =
                """
                {
                    "version": "1.2.3",
                    "instructions": {
                        "a.md": {
                            "version": "1.0",
                            "disabled": true,
                            "disabledRules": [
                                "x"
                            ]
                        }
                    },
                    "mcpTools": {
                        "t1": {
                            "disabled": true
                        },
                        "t2": {
                            "disabled": true
                        }
                    }
                }
                """ + "\n";

            // Act
            var text = Encoding.UTF8.GetString(ConfigFileFormat.Serialize(config, "1.2.3"));

            // Assert
            Assert.Equal(expected, text);
        }
    }

    public sealed class TryDeserialize
    {
        [Fact]
        public void Should_return_empty_for_empty_input()
        {
            // Act
            var ok = ConfigFileFormat.TryDeserialize([], out var config);

            // Assert
            Assert.Multiple(
                () => Assert.True(ok),
                () => Assert.True(config.IsEmpty));
        }

        [Fact]
        public void Should_fail_for_malformed_json()
        {
            // Arrange
            var bytes = Encoding.UTF8.GetBytes("not json");

            // Act
            var ok = ConfigFileFormat.TryDeserialize(bytes, out var config);

            // Assert
            Assert.Multiple(
                () => Assert.False(ok),
                () => Assert.True(config.IsEmpty));
        }

        [Fact]
        public void Should_round_trip_through_serialize()
        {
            // Arrange
            var original = ConfigFileFormat.Serialize(
                new JsonConfigFile
                {
                    McpTools = new Dictionary<string, JsonConfigFileMcpToolEntry>
                    {
                        ["t1"] = new() { Disabled = true },
                        ["t2"] = new() { Disabled = true },
                    },
                },
                "1.2.3");

            // Act
            ConfigFileFormat.TryDeserialize(original, out var parsed);
            var reserialized = ConfigFileFormat.Serialize(parsed, "1.2.3");

            // Assert
            Assert.Equal(original, reserialized);
        }

        [Fact]
        public void Should_normalize_empty_arrays_and_redundant_disabled_flags()
        {
            // Arrange
            var bytes = Encoding.UTF8.GetBytes(
                """
                {
                    "instructions": {
                        "a.md": { "disabled": false, "disabledRules": [] }
                    },
                    "mcpTools": {
                        "t1": { "disabled": false }
                    }
                }
                """);

            // Act
            ConfigFileFormat.TryDeserialize(bytes, out var config);

            // Assert
            Assert.Multiple(
                () => Assert.Null(config.Instructions!["a.md"].Disabled),
                () => Assert.Null(config.Instructions!["a.md"].DisabledRules),
                () => Assert.Null(config.McpTools!["t1"].Disabled));
        }

        [Fact]
        public void Should_drop_engine_with_empty_directories()
        {
            // Arrange
            var bytes = Encoding.UTF8.GetBytes("""{ "engine": { "instructions.overridesRoots": [] } }""");

            // Act
            ConfigFileFormat.TryDeserialize(bytes, out var config);

            // Assert
            Assert.Null(config.Engine);
        }

        [Fact]
        public void Should_preserve_engine_directories()
        {
            // Arrange
            var bytes = Encoding.UTF8.GetBytes(
                """{ "engine": { "instructions.overridesRoots": [".github", ".copilot"] } }""");

            // Act
            ConfigFileFormat.TryDeserialize(bytes, out var config);

            // Assert
            Assert.Equal([".github", ".copilot"], config.Engine!.InstructionsOverridesRoots);
        }
    }
}
