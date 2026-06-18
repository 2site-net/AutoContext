namespace AutoContext.Engine.Core.Tests.Rpc.Policies;

using System.Text.Json;

using AutoContext.Engine.Core.Features.McpTools.Snapshot;
using AutoContext.Engine.Core.Rpc.Results;
using AutoContext.Engine.Core.Tests.Support;
using AutoContext.Engine.Core.Tests.Support.Features.McpTools;
using AutoContext.Engine.Core.Tests.Support.Rpc;
using AutoContext.Engine.Core.Tests.Support.Rpc.Policies;
using AutoContext.Engine.Core.Tests.Support.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;
using AutoContext.Engine.Protocol.JsonRpc;
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
            Parameters =
            [
                new McpToolsRegistryParameterEntry
                {
                    Name = "content",
                    Type = "string",
                    Description = "Tool input.",
                    Required = true,
                },
            ],
        };

    private static McpToolsCategoryEntry Category(string name) =>
        new() { Name = name, Description = "A category." };

    private static JsonElement ContentBlock(string text) =>
        JsonSerializer.SerializeToElement(new { type = "text", text });

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

    [Fact]
    public async Task Should_return_not_found_when_tool_is_missing_for_McpTools_Invoke()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = DispatchPolicyTestFactory.Create(
            lifetime,
            mcpToolsRegistryAccessor: new FakeMcpToolsRegistryAccessor());
        var request = JsonRpcRequestTestFactory.BuildRequest(
            McpToolsMethods.Invoke,
            new JsonMcpToolsInvokeParams
            {
                Name = "missing_tool",
                Arguments = JsonSerializer.SerializeToElement(new { }),
            },
            ProtocolJsonContext.Default.JsonMcpToolsInvokeParams);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var payload = JsonSerializer.Deserialize(
            result.Response.Result!.Value,
            ProtocolJsonContext.Default.JsonMcpToolsInvokeResult)!;

        var notFound = Assert.IsType<JsonMcpToolsInvokeNotFoundResult>(payload);
        Assert.Equal("missing_tool", notFound.Name);
    }

    [Fact]
    public async Task Should_return_disabled_when_tool_is_muted_for_McpTools_Invoke()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var registry = new FakeMcpToolsRegistryAccessor(new McpToolsRegistry(
            [Category("CSharp")],
            [Tool("analyze_csharp_code")]));
        var config = new FakeConfigSnapshotAccessor
        {
            Current = ConfigSnapshot.Empty with
            {
                McpTools = [new ConfigMcpTool { Name = "analyze_csharp_code", Disabled = true }],
            },
        };

        var policy = DispatchPolicyTestFactory.Create(
            lifetime,
            configAccessor: config,
            mcpToolsRegistryAccessor: registry);

        var request = JsonRpcRequestTestFactory.BuildRequest(
            McpToolsMethods.Invoke,
            new JsonMcpToolsInvokeParams
            {
                Name = "analyze_csharp_code",
                Arguments = JsonSerializer.SerializeToElement(new { content = "sample" }),
            },
            ProtocolJsonContext.Default.JsonMcpToolsInvokeParams);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var payload = JsonSerializer.Deserialize(
            result.Response.Result!.Value,
            ProtocolJsonContext.Default.JsonMcpToolsInvokeResult)!;

        var disabled = Assert.IsType<JsonMcpToolsInvokeDisabledResult>(payload);
        Assert.Equal("analyze_csharp_code", disabled.Name);
    }

    [Fact]
    public async Task Should_return_schema_error_when_arguments_are_invalid_for_McpTools_Invoke()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var registry = new FakeMcpToolsRegistryAccessor(new McpToolsRegistry(
            [Category("CSharp")],
            [Tool("analyze_csharp_code")]));
        var policy = DispatchPolicyTestFactory.Create(
            lifetime,
            mcpToolsRegistryAccessor: registry);

        var request = JsonRpcRequestTestFactory.BuildRequest(
            McpToolsMethods.Invoke,
            new JsonMcpToolsInvokeParams
            {
                Name = "analyze_csharp_code",
                Arguments = JsonSerializer.SerializeToElement(new { wrong = "value" }),
            },
            ProtocolJsonContext.Default.JsonMcpToolsInvokeParams);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var payload = JsonSerializer.Deserialize(
            result.Response.Result!.Value,
            ProtocolJsonContext.Default.JsonMcpToolsInvokeResult)!;

        var schemaError = Assert.IsType<JsonMcpToolsInvokeSchemaErrorResult>(payload);
        Assert.Multiple(
            () => Assert.Equal("analyze_csharp_code", schemaError.Name),
            () => Assert.Collection(
                schemaError.Errors,
                first =>
                {
                    Assert.Equal("content", first.Path);
                    Assert.Equal("Required parameter is missing.", first.Message);
                },
                second =>
                {
                    Assert.Equal("wrong", second.Path);
                    Assert.Equal("Unknown parameter.", second.Message);
                }));
    }

    [Fact]
    public async Task Should_dispatch_to_invoker_and_return_ok_for_McpTools_Invoke()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var registry = new FakeMcpToolsRegistryAccessor(new McpToolsRegistry(
            [Category("CSharp")],
            [Tool("analyze_csharp_code")]));
        var invoker = new FakeMcpToolsInvoker
        {
            Result = new JsonMcpToolsInvokeOkResult
            {
                Name = "analyze_csharp_code",
                Content = [ContentBlock("ok")],
            },
        };

        var policy = DispatchPolicyTestFactory.Create(
            lifetime,
            mcpToolsRegistryAccessor: registry,
            mcpToolsInvoker: invoker);

        var request = JsonRpcRequestTestFactory.BuildRequest(
            McpToolsMethods.Invoke,
            new JsonMcpToolsInvokeParams
            {
                Name = "analyze_csharp_code",
                Arguments = JsonSerializer.SerializeToElement(new { content = "sample" }),
            },
            ProtocolJsonContext.Default.JsonMcpToolsInvokeParams);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var payload = JsonSerializer.Deserialize(
            result.Response.Result!.Value,
            ProtocolJsonContext.Default.JsonMcpToolsInvokeResult)!;

        var ok = Assert.IsType<JsonMcpToolsInvokeOkResult>(payload);
        Assert.Multiple(
            () => Assert.Equal(1, invoker.InvokeCallCount),
            () => Assert.Equal("analyze_csharp_code", invoker.LastTool?.Name),
            () => Assert.Equal(JsonValueKind.Object, invoker.LastArguments.ValueKind),
            () => Assert.Equal("analyze_csharp_code", ok.Name),
            () => Assert.Single(ok.Content));
    }

    [Fact]
    public async Task Should_surface_tool_error_when_invoker_returns_failure_for_McpTools_Invoke()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var registry = new FakeMcpToolsRegistryAccessor(new McpToolsRegistry(
            [Category("CSharp")],
            [Tool("analyze_csharp_code")]));
        var invoker = new FakeMcpToolsInvoker
        {
            Result = new JsonMcpToolsInvokeToolErrorResult
            {
                Name = "analyze_csharp_code",
                Content = [ContentBlock("failure")],
            },
        };

        var policy = DispatchPolicyTestFactory.Create(
            lifetime,
            mcpToolsRegistryAccessor: registry,
            mcpToolsInvoker: invoker);

        var request = JsonRpcRequestTestFactory.BuildRequest(
            McpToolsMethods.Invoke,
            new JsonMcpToolsInvokeParams
            {
                Name = "analyze_csharp_code",
                Arguments = JsonSerializer.SerializeToElement(new { content = "sample" }),
            },
            ProtocolJsonContext.Default.JsonMcpToolsInvokeParams);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var payload = JsonSerializer.Deserialize(
            result.Response.Result!.Value,
            ProtocolJsonContext.Default.JsonMcpToolsInvokeResult)!;

        var toolError = Assert.IsType<JsonMcpToolsInvokeToolErrorResult>(payload);
        Assert.Multiple(
            () => Assert.Equal("analyze_csharp_code", toolError.Name),
            () => Assert.True(toolError.IsError),
            () => Assert.Single(toolError.Content));
    }

    [Fact]
    public async Task Should_return_internal_error_when_invoker_throws_for_McpTools_Invoke()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var registry = new FakeMcpToolsRegistryAccessor(new McpToolsRegistry(
            [Category("CSharp")],
            [Tool("analyze_csharp_code")]));
        var invoker = new FakeMcpToolsInvoker
        {
            ThrowOnInvoke = new InvalidOperationException("worker dispatch faulted"),
        };

        var policy = DispatchPolicyTestFactory.Create(
            lifetime,
            mcpToolsRegistryAccessor: registry,
            mcpToolsInvoker: invoker);

        var request = JsonRpcRequestTestFactory.BuildRequest(
            McpToolsMethods.Invoke,
            new JsonMcpToolsInvokeParams
            {
                Name = "analyze_csharp_code",
                Arguments = JsonSerializer.SerializeToElement(new { content = "sample" }),
            },
            ProtocolJsonContext.Default.JsonMcpToolsInvokeParams);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        Assert.Multiple(
            () => Assert.NotNull(result.Response.Error),
            () => Assert.Equal(JsonRpcErrorCodes.InternalError, result.Response.Error!.Code));
    }
}
