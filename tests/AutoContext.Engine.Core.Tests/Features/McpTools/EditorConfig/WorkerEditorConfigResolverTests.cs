namespace AutoContext.Engine.Core.Tests.Features.McpTools.EditorConfig;

using System.Text;
using System.Text.Json;

using AutoContext.Engine.Core.Features.McpTools.EditorConfig;

public sealed class WorkerEditorConfigResolverTests
{
    public sealed class ShouldSkip
    {
        [Fact]
        public void Should_skip_when_no_keys_are_declared()
            => Assert.True(WorkerEditorConfigResolver.ShouldSkip("/repo/File.cs", []));

        [Fact]
        public void Should_skip_when_file_path_is_null()
            => Assert.True(WorkerEditorConfigResolver.ShouldSkip(null, ["csharp_prefer_braces"]));

        [Fact]
        public void Should_skip_when_file_path_is_whitespace()
            => Assert.True(WorkerEditorConfigResolver.ShouldSkip("   ", ["csharp_prefer_braces"]));

        [Fact]
        public void Should_not_skip_when_path_and_keys_are_present()
            => Assert.False(
                WorkerEditorConfigResolver.ShouldSkip("/repo/File.cs", ["csharp_prefer_braces"]));
    }

    public sealed class BuildRequestBytes
    {
        [Fact]
        public void Should_build_the_get_editorconfig_rules_request_envelope()
        {
            // Act
            var bytes = WorkerEditorConfigResolver.BuildRequestBytes(
                "/repo/File.cs",
                ["csharp_prefer_braces", "dotnet_sort_system_directives_first"],
                "abc123");

            // Assert
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;

            Assert.Multiple(
                () => Assert.Equal("get_editorconfig_rules", root.GetProperty("mcpTask").GetString()),
                () => Assert.Equal("abc123", root.GetProperty("correlationId").GetString()),
                () => Assert.Equal(
                    "/repo/File.cs",
                    root.GetProperty("data").GetProperty("path").GetString()),
                () => Assert.Equal(
                    JsonValueKind.Object,
                    root.GetProperty("editorconfig").ValueKind),
                () => Assert.Empty(root.GetProperty("editorconfig").EnumerateObject()));
        }

        [Fact]
        public void Should_emit_keys_in_declaration_order()
        {
            // Act
            var bytes = WorkerEditorConfigResolver.BuildRequestBytes(
                "/repo/File.cs",
                ["b_key", "a_key"],
                "abc123");

            // Assert
            using var document = JsonDocument.Parse(bytes);
            var keys = document.RootElement.GetProperty("data").GetProperty("keys");

            Assert.Collection(
                keys.EnumerateArray(),
                first => Assert.Equal("b_key", first.GetString()),
                second => Assert.Equal("a_key", second.GetString()));
        }
    }

    public sealed class ParseResolvedMap
    {
        [Fact]
        public void Should_map_string_values_when_status_is_ok()
        {
            // Arrange
            var bytes = Encoding.UTF8.GetBytes(
                """{"status":"ok","output":{"csharp_prefer_braces":"true","indent_size":"4"}}""");

            // Act
            var map = WorkerEditorConfigResolver.ParseResolvedMap(bytes);

            // Assert
            Assert.Multiple(
                () => Assert.Equal(2, map.Count),
                () => Assert.Equal("true", map["csharp_prefer_braces"]),
                () => Assert.Equal("4", map["indent_size"]));
        }

        [Fact]
        public void Should_return_empty_when_status_is_error()
        {
            // Arrange
            var bytes = Encoding.UTF8.GetBytes("""{"status":"error","error":"boom"}""");

            // Act
            var map = WorkerEditorConfigResolver.ParseResolvedMap(bytes);

            // Assert
            Assert.Empty(map);
        }

        [Fact]
        public void Should_return_empty_when_output_is_not_an_object()
        {
            // Arrange
            var bytes = Encoding.UTF8.GetBytes("""{"status":"ok","output":"not-an-object"}""");

            // Act
            var map = WorkerEditorConfigResolver.ParseResolvedMap(bytes);

            // Assert
            Assert.Empty(map);
        }

        [Fact]
        public void Should_skip_non_string_values()
        {
            // Arrange
            var bytes = Encoding.UTF8.GetBytes(
                """{"status":"ok","output":{"good":"yes","number":4,"flag":true,"nothing":null}}""");

            // Act
            var map = WorkerEditorConfigResolver.ParseResolvedMap(bytes);

            // Assert
            Assert.Multiple(
                () => Assert.Single(map),
                () => Assert.Equal("yes", map["good"]));
        }
    }
}
