namespace AutoContext.Engine.Core.Tests.Features.McpTools;

using System.Text.Json;

using AutoContext.Engine.Core.Features.McpTools;

public sealed class McpToolsInvokerTests
{
    public sealed class TryGetFilePath
    {
        [Fact]
        public void Should_return_the_file_path_when_present()
        {
            // Arrange
            var arguments = JsonDocument.Parse(
                """{"content":"x","filePath":"/repo/File.cs"}""").RootElement;

            // Act
            var filePath = McpToolsInvoker.TryGetFilePath(arguments);

            // Assert
            Assert.Equal("/repo/File.cs", filePath);
        }

        [Fact]
        public void Should_return_null_when_absent()
            => Assert.Null(
                McpToolsInvoker.TryGetFilePath(JsonDocument.Parse("""{"content":"x"}""").RootElement));

        [Fact]
        public void Should_return_null_when_blank()
            => Assert.Null(
                McpToolsInvoker.TryGetFilePath(JsonDocument.Parse("""{"filePath":"   "}""").RootElement));

        [Fact]
        public void Should_return_null_when_not_a_string()
            => Assert.Null(
                McpToolsInvoker.TryGetFilePath(JsonDocument.Parse("""{"filePath":42}""").RootElement));

        [Fact]
        public void Should_return_null_when_arguments_is_not_an_object()
            => Assert.Null(
                McpToolsInvoker.TryGetFilePath(JsonDocument.Parse("\"scalar\"").RootElement));
    }

    public sealed class BuildRequestBytes
    {
        [Fact]
        public void Should_inject_the_resolved_editorconfig_map()
        {
            // Arrange
            var arguments = JsonDocument.Parse("""{"content":"class C {}"}""").RootElement;
            var editorconfig = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["csharp_prefer_braces"] = "true",
                ["indent_size"] = "4",
            };

            // Act
            var bytes = McpToolsInvoker.BuildRequestBytes(
                "analyze_csharp_code_style", arguments, editorconfig, "abc123");

            // Assert
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            var editorconfigElement = root.GetProperty("editorconfig");

            Assert.Multiple(
                () => Assert.Equal(
                    "analyze_csharp_code_style", root.GetProperty("mcpTask").GetString()),
                () => Assert.Equal("abc123", root.GetProperty("correlationId").GetString()),
                () => Assert.Equal(
                    "class C {}", root.GetProperty("data").GetProperty("content").GetString()),
                () => Assert.Equal(
                    "true", editorconfigElement.GetProperty("csharp_prefer_braces").GetString()),
                () => Assert.Equal(
                    "4", editorconfigElement.GetProperty("indent_size").GetString()));
        }

        [Fact]
        public void Should_emit_an_empty_editorconfig_object_when_the_map_is_empty()
        {
            // Arrange
            var arguments = JsonDocument.Parse("""{"content":"class C {}"}""").RootElement;

            // Act
            var bytes = McpToolsInvoker.BuildRequestBytes(
                "analyze_csharp_code_style",
                arguments,
                new Dictionary<string, string>(StringComparer.Ordinal),
                "abc123");

            // Assert
            using var document = JsonDocument.Parse(bytes);
            var editorconfigElement = document.RootElement.GetProperty("editorconfig");

            Assert.Multiple(
                () => Assert.Equal(JsonValueKind.Object, editorconfigElement.ValueKind),
                () => Assert.Empty(editorconfigElement.EnumerateObject()));
        }
    }
}
