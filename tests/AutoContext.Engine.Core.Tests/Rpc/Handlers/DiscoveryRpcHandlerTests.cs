namespace AutoContext.Engine.Core.Tests.Rpc.Handlers;

using System.Text.Json;

using AutoContext.Engine.Core.Features.Discovery;
using AutoContext.Engine.Core.Features.Instructions.Snapshot;
using AutoContext.Engine.Core.Features.McpTools.Snapshot;
using AutoContext.Engine.Core.Rpc.Handlers;
using AutoContext.Engine.Core.Rpc.Results;
using AutoContext.Engine.Core.Tests.Support.Features.Instructions;
using AutoContext.Engine.Core.Tests.Support.Features.McpTools;
using AutoContext.Engine.Core.Tests.Support.Rpc;
using AutoContext.Engine.Core.Tests.Support.Workspace.Config;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages.Discovery;
using AutoContext.Engine.Protocol.Serialization;

using Microsoft.Extensions.Logging.Abstractions;

public sealed class DiscoveryRpcHandlerTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_service()
        => Assert.Throws<ArgumentNullException>(() => new DiscoveryRpcHandler(
            discoveryService: null!,
            logger: NullLogger<DiscoveryRpcHandler>.Instance));

    [Fact]
    public void Should_throw_when_constructed_with_null_logger()
        => Assert.Throws<ArgumentNullException>(() => new DiscoveryRpcHandler(
            CreateService(),
            logger: null!));

    [Fact]
    public void Should_serve_the_two_discovery_methods()
    {
        var handler = new DiscoveryRpcHandler(CreateService(), NullLogger<DiscoveryRpcHandler>.Instance);

        Assert.Equal(
            [DiscoveryMethods.RouteForPrompt, DiscoveryMethods.RouteForTool],
            handler.Methods);
    }

    [Fact]
    public async Task Should_answer_route_for_prompt_with_the_routed_tools_and_files()
    {
        var handler = new DiscoveryRpcHandler(CreateService(), NullLogger<DiscoveryRpcHandler>.Instance);
        var request = JsonRpcRequestTestFactory.BuildRequest(
            DiscoveryMethods.RouteForPrompt,
            new JsonDiscoveryRouteForPromptParams { Prompt = "some C# in Foo.cs" },
            ProtocolJsonContext.Default.JsonDiscoveryRouteForPromptParams);

        var result = Assert.IsType<UnaryHandlerResult>(
            await handler.InvokeAsync(request, TestContext.Current.CancellationToken));
        var payload = result.Response.Result!.Value.Deserialize(
            ProtocolJsonContext.Default.JsonDiscoveryRouteForPromptResult)!;

        Assert.Multiple(
            () => Assert.Equal(["analyze_csharp_code"], payload.Tools),
            () => Assert.Equal(["lang-csharp.instructions.md"], payload.Instructions));
    }

    [Fact]
    public async Task Should_answer_route_for_tool_with_the_domain_files()
    {
        var handler = new DiscoveryRpcHandler(CreateService(), NullLogger<DiscoveryRpcHandler>.Instance);
        var request = JsonRpcRequestTestFactory.BuildRequest(
            DiscoveryMethods.RouteForTool,
            new JsonDiscoveryRouteForToolParams { Name = "analyze_csharp_code" },
            ProtocolJsonContext.Default.JsonDiscoveryRouteForToolParams);

        var result = Assert.IsType<UnaryHandlerResult>(
            await handler.InvokeAsync(request, TestContext.Current.CancellationToken));
        var payload = result.Response.Result!.Value.Deserialize(
            ProtocolJsonContext.Default.JsonDiscoveryRouteForToolResult)!;

        Assert.Equal(["lang-csharp.instructions.md"], payload.Instructions);
    }

    [Fact]
    public async Task Should_reply_invalid_params_when_the_prompt_payload_is_malformed()
    {
        var handler = new DiscoveryRpcHandler(CreateService(), NullLogger<DiscoveryRpcHandler>.Instance);
        var request = new JsonRpcRequest
        {
            Method = DiscoveryMethods.RouteForPrompt,
            Id = JsonSerializer.SerializeToElement(1),
            Params = JsonSerializer.SerializeToElement("not-an-object"),
        };

        var result = Assert.IsType<UnaryHandlerResult>(
            await handler.InvokeAsync(request, TestContext.Current.CancellationToken));

        Assert.Multiple(
            () => Assert.NotNull(result.Response.Error),
            () => Assert.Equal(JsonRpcErrorCodes.InvalidParams, result.Response.Error!.Code));
    }

    private static DiscoveryService CreateService()
    {
        var registry = new McpToolsRegistry(
            [
                new McpToolsCategoryEntry { Name = ".NET", Description = ".NET" },
                new McpToolsCategoryEntry { Name = "C#", Description = "C#", Parent = ".NET" },
            ],
            [
                new McpToolsRegistryEntry
                {
                    Name = "analyze_csharp_code",
                    Category = "C#",
                    WorkerId = "dotnet",
                    ModelDescription = "Analyze C#.",
                    DisplayDescription = "Analyze C#.",
                    Parameters = [],
                    ActivationFlags = ["hasDotNet", "hasCSharp"],
                },
            ]);

        var file = new InstructionsFileManifestEntry
        {
            Key = "lang-csharp",
            FileName = "lang-csharp.instructions.md",
            Name = "lang-csharp (v1.0.0)",
            Version = "1.0.0",
            Description = "C#",
            HasChangelog = false,
            ContentHash = "sha256:0",
            AlwaysAttached = false,
            Extensions = ["cs"],
            ActivationFlags = ["hasDotNet", "hasCSharp"],
        };

        return new DiscoveryService(
            new FakeMcpToolsRegistryAccessor(registry),
            new FakeInstructionsManifestAccessor(file),
            new FakeConfigSnapshotAccessor());
    }
}
