namespace AutoContext.Mcp.Tools.Tests.Registry;

using System.Collections.Generic;
using System.Text.Json;

using AutoContext.Mcp.Tools.Registry;

public sealed class InputSchemaBuilderTests
{
    [Fact]
    public void Should_emit_object_root_with_typed_properties()
    {
        // Arrange
        var parameters = new Dictionary<string, McpToolParameter>
        {
            ["content"] = new()
            {
                Type = "string",
                Description = "The C# source code to check.",
                Required = true,
            },
        };

        // Act
        var schema = InputSchemaBuilder.Build(parameters);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("object", schema.GetProperty("type").GetString()),
            () => Assert.Equal(
                "string",
                schema.GetProperty("properties").GetProperty("content").GetProperty("type").GetString()),
            () => Assert.Equal(
                "The C# source code to check.",
                schema.GetProperty("properties").GetProperty("content").GetProperty("description").GetString()));
    }

    [Fact]
    public void Should_emit_all_parameter_names()
    {
        // Arrange
        var parameters = new Dictionary<string, McpToolParameter>
        {
            ["c"] = new() { Type = "number", Description = "C", Required = true },
            ["b"] = new() { Type = "string", Description = "B" },
            ["a"] = new() { Type = "string", Description = "A", Required = true },
        };

        // Act
        var schema = InputSchemaBuilder.Build(parameters);
        var required = schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()!).ToHashSet();
        var properties = schema.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToHashSet();

        // Assert — JSON Schema treats `required` as a set and `properties` as an unordered object.
        Assert.Multiple(
            () => Assert.Equal(["a", "c"], required),
            () => Assert.Equal(["a", "b", "c"], properties));
    }

    [Fact]
    public void Should_omit_required_array_when_no_parameter_is_required()
    {
        // Arrange
        var parameters = new Dictionary<string, McpToolParameter>
        {
            ["x"] = new() { Type = "string", Description = "X" },
        };

        // Act
        var schema = InputSchemaBuilder.Build(parameters);

        // Assert
        Assert.False(schema.TryGetProperty("required", out _));
    }

    [Fact]
    public void Should_emit_empty_properties_object_when_parameters_are_empty()
    {
        // Arrange
        var parameters = new Dictionary<string, McpToolParameter>();

        // Act
        var schema = InputSchemaBuilder.Build(parameters);

        // Assert
        Assert.Multiple(
            () => Assert.Equal("object", schema.GetProperty("type").GetString()),
            () => Assert.Equal(JsonValueKind.Object, schema.GetProperty("properties").ValueKind),
            () => Assert.Empty(schema.GetProperty("properties").EnumerateObject()),
            () => Assert.False(schema.TryGetProperty("required", out _)));
    }

    [Fact]
    public void Should_throw_when_parameters_is_null()
    {
        Assert.Throws<ArgumentNullException>(() => InputSchemaBuilder.Build(null!));
    }
}
