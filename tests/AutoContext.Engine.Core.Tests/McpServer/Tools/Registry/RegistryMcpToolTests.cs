namespace AutoContext.Engine.Core.Tests.McpServer.Tools.Registry;

using System.Collections.Generic;
using System.Text.Json;

using AutoContext.Engine.Core.Features.McpTools.Snapshot;
using AutoContext.Engine.Core.McpServer.Tools.Registry;
using AutoContext.Engine.Core.Tests.Support.Rpc;
using AutoContext.Engine.Protocol.Messages.McpTools;

public sealed class RegistryMcpToolTests
{
    private static McpToolsRegistryEntry Entry(string name) => new()
    {
        Name = name,
        Category = "C#",
        WorkerId = "dotnet",
        ModelDescription = $"{name} description",
        DisplayDescription = $"{name} display",
        Parameters =
        [
            new McpToolsRegistryParameterEntry
            {
                Name = "content",
                Type = "string",
                Description = "The source text.",
                Required = true,
            },
        ],
    };

    [Fact]
    public void Should_build_the_descriptor_from_the_entry()
    {
        // Act
        var tool = new RegistryMcpTool(Entry("analyze_a"), new FakeRpcMethodHandler());

        // Assert
        Assert.Multiple(
            () => Assert.Equal("analyze_a", tool.Descriptor.Name),
            () => Assert.Equal("analyze_a description", tool.Descriptor.Description));
    }

    [Fact]
    public async Task Should_marshal_into_mcp_tools_invoke_with_its_name()
    {
        // Arrange
        var handler = new FakeRpcMethodHandler();
        var tool = new RegistryMcpTool(Entry("analyze_a"), handler);
        var arguments = new Dictionary<string, JsonElement>
        {
            ["content"] = JsonSerializer.SerializeToElement("class C {}"),
        };

        // Act
        await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(McpToolsMethods.Invoke, handler.LastRequest!.Method),
            () => Assert.Equal(
                "analyze_a", handler.LastRequest!.Params!.Value.GetProperty("name").GetString()));
    }
}
