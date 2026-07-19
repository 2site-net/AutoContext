namespace AutoContext.Engine.Core.Tests.McpServer.Tools.Intrinsics;

using System.Collections.Generic;
using System.Text.Json;

using AutoContext.Engine.Core.McpServer.Tools.Intrinsics;
using AutoContext.Engine.Core.Tests.Support.Rpc;
using AutoContext.Engine.Protocol.Messages.Instructions;

public sealed class InstructionsToolsTests
{
    [Fact]
    public void Should_name_each_instruction_tool()
        => Assert.Multiple(
            () => Assert.Equal(
                "instructions_list", new InstructionsListTool(new FakeRpcMethodHandler()).Descriptor.Name),
            () => Assert.Equal(
                "instructions_search_content",
                new InstructionsSearchContentTool(new FakeRpcMethodHandler()).Descriptor.Name),
            () => Assert.Equal(
                "instructions_get", new InstructionsGetTool(new FakeRpcMethodHandler()).Descriptor.Name));

    [Fact]
    public async Task Should_marshal_list_into_the_instructions_handler()
    {
        // Arrange
        var handler = new FakeRpcMethodHandler();
        var tool = new InstructionsListTool(handler);

        // Act
        await tool.InvokeAsync(arguments: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InstructionsMethods.List, handler.LastRequest!.Method);
    }

    [Fact]
    public async Task Should_marshal_search_content_with_its_query()
    {
        // Arrange
        var handler = new FakeRpcMethodHandler();
        var tool = new InstructionsSearchContentTool(handler);
        var arguments = new Dictionary<string, JsonElement>
        {
            ["query"] = JsonSerializer.SerializeToElement("needle"),
        };

        // Act
        await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(InstructionsMethods.SearchContent, handler.LastRequest!.Method),
            () => Assert.Equal(
                "needle", handler.LastRequest!.Params!.Value.GetProperty("query").GetString()));
    }

    [Fact]
    public async Task Should_marshal_get_with_its_name()
    {
        // Arrange
        var handler = new FakeRpcMethodHandler();
        var tool = new InstructionsGetTool(handler);
        var arguments = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("testing"),
        };

        // Act
        await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert
        Assert.Multiple(
            () => Assert.Equal(InstructionsMethods.Get, handler.LastRequest!.Method),
            () => Assert.Equal(
                "testing", handler.LastRequest!.Params!.Value.GetProperty("name").GetString()));
    }
}
