namespace AutoContext.Engine.Protocol.Tests.Messages.Discovery;

using System.Text.Json;

using AutoContext.Engine.Protocol.Messages.Discovery;
using AutoContext.Engine.Protocol.Serialization;

public sealed class DiscoveryMessagesTests
{
    [Fact]
    public void Should_expose_route_for_prompt_method_constant_matching_design()
        => Assert.Equal("Discovery.RouteForPrompt", DiscoveryMethods.RouteForPrompt);

    [Fact]
    public void Should_expose_route_for_tool_method_constant_matching_design()
        => Assert.Equal("Discovery.RouteForTool", DiscoveryMethods.RouteForTool);

    [Fact]
    public void Should_serialize_prompt_result_with_camelCase_keys()
    {
        var result = new JsonDiscoveryRouteForPromptResult
        {
            MatchedCategories = ["C#"],
            MatchedExtensions = [".cs"],
            Tools = ["analyze_csharp_code"],
            Instructions = ["lang-csharp.instructions.md"],
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            result, ProtocolJsonContext.Default.JsonDiscoveryRouteForPromptResult);

        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;

        Assert.Multiple(
            () => Assert.Equal(
                ["C#"],
                root.GetProperty("matchedCategories").EnumerateArray().Select(e => e.GetString())),
            () => Assert.Equal(
                [".cs"],
                root.GetProperty("matchedExtensions").EnumerateArray().Select(e => e.GetString())),
            () => Assert.Equal(
                ["analyze_csharp_code"],
                root.GetProperty("tools").EnumerateArray().Select(e => e.GetString())),
            () => Assert.Equal(
                ["lang-csharp.instructions.md"],
                root.GetProperty("instructions").EnumerateArray().Select(e => e.GetString())));
    }

    [Fact]
    public void Should_round_trip_prompt_result()
    {
        var result = new JsonDiscoveryRouteForPromptResult
        {
            MatchedCategories = [".NET", "C#"],
            MatchedExtensions = [".cs"],
            Tools = ["analyze_csharp_code", "analyze_nuget_references"],
            Instructions = ["lang-csharp.instructions.md"],
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            result, ProtocolJsonContext.Default.JsonDiscoveryRouteForPromptResult);
        var restored = JsonSerializer.Deserialize(
            bytes, ProtocolJsonContext.Default.JsonDiscoveryRouteForPromptResult)!;

        Assert.Multiple(
            () => Assert.Equal(result.MatchedCategories, restored.MatchedCategories),
            () => Assert.Equal(result.MatchedExtensions, restored.MatchedExtensions),
            () => Assert.Equal(result.Tools, restored.Tools),
            () => Assert.Equal(result.Instructions, restored.Instructions));
    }

    [Fact]
    public void Should_round_trip_tool_result()
    {
        var result = new JsonDiscoveryRouteForToolResult
        {
            Instructions = ["lang-csharp.instructions.md", "dotnet-testing.instructions.md"],
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            result, ProtocolJsonContext.Default.JsonDiscoveryRouteForToolResult);
        var restored = JsonSerializer.Deserialize(
            bytes, ProtocolJsonContext.Default.JsonDiscoveryRouteForToolResult)!;

        Assert.Equal(result.Instructions, restored.Instructions);
    }

    [Fact]
    public void Should_round_trip_prompt_params()
    {
        var parameters = new JsonDiscoveryRouteForPromptParams { Prompt = "fix my C#" };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            parameters, ProtocolJsonContext.Default.JsonDiscoveryRouteForPromptParams);
        var restored = JsonSerializer.Deserialize(
            bytes, ProtocolJsonContext.Default.JsonDiscoveryRouteForPromptParams)!;

        Assert.Equal("fix my C#", restored.Prompt);
    }
}
