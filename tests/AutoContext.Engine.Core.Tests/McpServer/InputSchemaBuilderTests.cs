namespace AutoContext.Engine.Core.Tests.McpServer;

using System.Linq;

using AutoContext.Engine.Core.Features.McpTools.Snapshot;
using AutoContext.Engine.Core.McpServer;

public sealed class InputSchemaBuilderTests
{
    [Fact]
    public void Should_render_an_object_schema_with_each_parameter()
    {
        // Act
        var schema = InputSchemaBuilder.Build(
        [
            new McpToolsRegistryParameterEntry
            {
                Name = "content",
                Type = "string",
                Description = "The source text.",
                Required = true,
            },
            new McpToolsRegistryParameterEntry
            {
                Name = "maxIssues",
                Type = "number",
                Description = "Issue cap.",
                Required = false,
            },
        ]);

        // Assert
        var properties = schema.GetProperty("properties");
        Assert.Multiple(
            () => Assert.Equal("object", schema.GetProperty("type").GetString()),
            () => Assert.Equal("string", properties.GetProperty("content").GetProperty("type").GetString()),
            () => Assert.Equal(
                "The source text.", properties.GetProperty("content").GetProperty("description").GetString()),
            () => Assert.Equal("number", properties.GetProperty("maxIssues").GetProperty("type").GetString()));
    }

    [Fact]
    public void Should_list_only_required_parameters_in_the_required_array()
    {
        // Act
        var schema = InputSchemaBuilder.Build(
        [
            new McpToolsRegistryParameterEntry
            {
                Name = "content",
                Type = "string",
                Description = "The source text.",
                Required = true,
            },
            new McpToolsRegistryParameterEntry
            {
                Name = "maxIssues",
                Type = "number",
                Description = "Issue cap.",
                Required = false,
            },
        ]);

        // Assert
        var required = schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()!).ToArray();
        Assert.Equal(["content"], required);
    }

    [Fact]
    public void Should_omit_the_required_array_when_no_parameter_is_required()
    {
        // Act
        var schema = InputSchemaBuilder.Build(
        [
            new McpToolsRegistryParameterEntry
            {
                Name = "maxIssues",
                Type = "number",
                Description = "Issue cap.",
                Required = false,
            },
        ]);

        // Assert
        Assert.False(schema.TryGetProperty("required", out _));
    }
}
