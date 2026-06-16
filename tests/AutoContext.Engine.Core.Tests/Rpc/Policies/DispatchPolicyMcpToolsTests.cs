namespace AutoContext.Engine.Core.Tests.Rpc.Policies;

using System.Text.Json;

using AutoContext.Engine.Core.Features.McpTools.Snapshot;
using AutoContext.Engine.Core.Rpc.Results;
using AutoContext.Engine.Core.Tests.Support.Features.McpTools;
using AutoContext.Engine.Core.Tests.Support.Lifecycle;
using AutoContext.Engine.Core.Tests.Support.Rpc;
using AutoContext.Engine.Core.Tests.Support.Rpc.Policies;
using AutoContext.Engine.Core.Tests.Support.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;
using AutoContext.Engine.Protocol.Messages.McpTools;
using AutoContext.Engine.Protocol.Serialization;

public sealed class DispatchPolicyMcpToolsTests
{
    private static McpToolsRegistryEntry Tool(
        string name,
        string category = "CSharp",
        string workerId = "dotnet",
        string modelDescription = "A tool.") =>
        new()
        {
            Name = name,
            Category = category,
            WorkerId = workerId,
            ModelDescription = modelDescription,
            DisplayDescription = "A sample tool.",
            Parameters = [],
        };

    private static McpToolsCategoryEntry Category(string name) =>
        new() { Name = name, Description = "A category." };

    [Fact]
    public async Task Should_return_a_row_per_registry_tool_for_McpTools_List()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var registry = new FakeMcpToolsRegistryAccessor(new McpToolsRegistry(
            [Category("CSharp")],
            [Tool("analyze_csharp_code"), Tool("analyze_nuget_references")]));
        var policy = DispatchPolicyTestFactory.Create(lifetime, mcpToolsRegistryAccessor: registry);
        var request = JsonRpcRequestTestFactory.BuildRequest(McpToolsMethods.List);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var payload = JsonSerializer.Deserialize(
            result.Response.Result!.Value, ProtocolJsonContext.Default.JsonMcpToolsListResult)!;
        Assert.Collection(
            payload.Tools,
            first => Assert.Equal("analyze_csharp_code", first.Name),
            second => Assert.Equal("analyze_nuget_references", second.Name));
    }

    [Fact]
    public async Task Should_project_every_field_for_McpTools_List()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var registry = new FakeMcpToolsRegistryAccessor(new McpToolsRegistry(
            [Category("CSharp")],
            [Tool(
                "analyze_csharp_code",
                category: "CSharp",
                workerId: "dotnet",
                modelDescription: "Analyzes C# code.")]));
        var policy = DispatchPolicyTestFactory.Create(lifetime, mcpToolsRegistryAccessor: registry);
        var request = JsonRpcRequestTestFactory.BuildRequest(McpToolsMethods.List);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var row = JsonSerializer.Deserialize(
            result.Response.Result!.Value, ProtocolJsonContext.Default.JsonMcpToolsListResult)!.Tools[0];
        Assert.Multiple(
            () => Assert.Equal("analyze_csharp_code", row.Key),
            () => Assert.Equal("analyze_csharp_code", row.Name),
            () => Assert.Equal("Analyzes C# code.", row.Description),
            () => Assert.Equal("dotnet", row.WorkerId),
            () => Assert.Equal("CSharp", row.Category),
            () => Assert.False(row.Disabled));
    }

    [Fact]
    public async Task Should_mark_row_disabled_when_config_disables_tool_for_McpTools_List()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var registry = new FakeMcpToolsRegistryAccessor(new McpToolsRegistry(
            [Category("CSharp")],
            [Tool("analyze_csharp_code"), Tool("analyze_nuget_references")]));
        var config = new FakeConfigSnapshotAccessor
        {
            Current = ConfigSnapshot.Empty with
            {
                McpTools = [new ConfigMcpTool { Name = "analyze_csharp_code", Disabled = true }],
            },
        };
        var policy = DispatchPolicyTestFactory.Create(
            lifetime, configAccessor: config, mcpToolsRegistryAccessor: registry);
        var request = JsonRpcRequestTestFactory.BuildRequest(McpToolsMethods.List);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var payload = JsonSerializer.Deserialize(
            result.Response.Result!.Value, ProtocolJsonContext.Default.JsonMcpToolsListResult)!;
        Assert.Multiple(
            () => Assert.True(payload.Tools[0].Disabled),
            () => Assert.False(payload.Tools[1].Disabled));
    }

    [Fact]
    public async Task Should_return_no_rows_when_registry_is_empty_for_McpTools_List()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = DispatchPolicyTestFactory.Create(
            lifetime, mcpToolsRegistryAccessor: new FakeMcpToolsRegistryAccessor());
        var request = JsonRpcRequestTestFactory.BuildRequest(McpToolsMethods.List);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var payload = JsonSerializer.Deserialize(
            result.Response.Result!.Value, ProtocolJsonContext.Default.JsonMcpToolsListResult)!;
        Assert.Empty(payload.Tools);
    }
}
