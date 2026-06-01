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
                        Enabled = false,
                        DisabledInstructions = ["x"],
                    },
                },
                McpTools = new Dictionary<string, JsonConfigFileMcpToolValue>
                {
                    ["t1"] = JsonConfigFileMcpToolValue.Disabled,
                    ["t2"] = JsonConfigFileMcpToolValue.FromEntry(
                        new JsonConfigFileMcpToolEntry { Enabled = false, DisabledTasks = ["k"] }),
                },
            };

            var expected = string.Join(
                "\n",
                "{",
                "    \"version\": \"1.2.3\",",
                "    \"instructions\": {",
                "        \"a.md\": {",
                "            \"version\": \"1.0\",",
                "            \"enabled\": false,",
                "            \"disabledInstructions\": [",
                "                \"x\"",
                "            ]",
                "        }",
                "    },",
                "    \"mcpTools\": {",
                "        \"t1\": false,",
                "        \"t2\": {",
                "            \"enabled\": false,",
                "            \"disabledTasks\": [",
                "                \"k\"",
                "            ]",
                "        }",
                "    }",
                "}") + "\n";

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
                    McpTools = new Dictionary<string, JsonConfigFileMcpToolValue>
                    {
                        ["t1"] = JsonConfigFileMcpToolValue.Disabled,
                        ["t2"] = JsonConfigFileMcpToolValue.FromEntry(
                            new JsonConfigFileMcpToolEntry { Enabled = false, DisabledTasks = ["k"] }),
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
        public void Should_normalize_empty_arrays_and_redundant_enabled_flags()
        {
            // Arrange
            var bytes = Encoding.UTF8.GetBytes(
                """
                {
                    "instructions": {
                        "a.md": { "enabled": true, "disabledInstructions": [] }
                    },
                    "mcpTools": {
                        "t1": { "disabledTasks": [] }
                    }
                }
                """);

            // Act
            ConfigFileFormat.TryDeserialize(bytes, out var config);

            // Assert
            Assert.Multiple(
                () => Assert.Null(config.Instructions!["a.md"].Enabled),
                () => Assert.Null(config.Instructions!["a.md"].DisabledInstructions),
                () => Assert.Null(config.McpTools!["t1"].Entry!.DisabledTasks));
        }

        [Fact]
        public void Should_preserve_shorthand_disabled_tool()
        {
            // Arrange
            var bytes = Encoding.UTF8.GetBytes("""{ "mcpTools": { "t1": false } }""");

            // Act
            ConfigFileFormat.TryDeserialize(bytes, out var config);

            // Assert
            Assert.True(config.McpTools!["t1"].IsShorthandDisabled);
        }
    }
}
