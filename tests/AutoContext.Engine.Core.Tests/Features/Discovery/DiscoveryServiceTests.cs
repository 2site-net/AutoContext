namespace AutoContext.Engine.Core.Tests.Features.Discovery;

using AutoContext.Engine.Core.Features.Discovery;
using AutoContext.Engine.Core.Features.Instructions.Snapshot;
using AutoContext.Engine.Core.Features.McpTools.Snapshot;
using AutoContext.Engine.Core.Tests.Support.Features.Instructions;
using AutoContext.Engine.Core.Tests.Support.Features.McpTools;
using AutoContext.Engine.Core.Tests.Support.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;

public sealed class DiscoveryServiceTests
{
    [Fact]
    public void Should_reject_a_null_registry_accessor()
        => Assert.Throws<ArgumentNullException>(() => new DiscoveryService(
            null!,
            new FakeInstructionsManifestAccessor(),
            new FakeConfigSnapshotAccessor()));

    [Fact]
    public void Should_reject_a_null_manifest_accessor()
        => Assert.Throws<ArgumentNullException>(() => new DiscoveryService(
            new FakeMcpToolsRegistryAccessor(),
            null!,
            new FakeConfigSnapshotAccessor()));

    [Fact]
    public void Should_reject_a_null_config_accessor()
        => Assert.Throws<ArgumentNullException>(() => new DiscoveryService(
            new FakeMcpToolsRegistryAccessor(),
            new FakeInstructionsManifestAccessor(),
            null!));

    [Fact]
    public void Should_route_a_prompt_to_matched_categories_extensions_tools_and_files()
    {
        var service = CreateService(
            BuildRegistry(),
            [CSharpFile()]);

        var result = service.RouteForPrompt("fix my C# in Foo.cs");

        Assert.Multiple(
            () => Assert.Equal(["C#"], result.MatchedCategories),
            () => Assert.Equal([".cs"], result.MatchedExtensions),
            () => Assert.Equal(["analyze_csharp_code"], result.Tools),
            () => Assert.Equal(["lang-csharp.instructions.md"], result.Instructions));
    }

    [Fact]
    public void Should_exclude_a_disabled_tool_from_a_prompt_route()
    {
        var config = new ConfigSnapshot
        {
            McpTools = [new ConfigMcpTool { Name = "analyze_csharp_code", Disabled = true }],
        };
        var service = CreateService(BuildRegistry(), [CSharpFile()], config);

        var result = service.RouteForPrompt("fix my C# in Foo.cs");

        Assert.Multiple(
            () => Assert.Empty(result.Tools),
            () => Assert.Equal(["lang-csharp.instructions.md"], result.Instructions));
    }

    [Fact]
    public void Should_exclude_a_disabled_instruction_file_from_a_prompt_route()
    {
        var config = new ConfigSnapshot
        {
            Instructions = [new ConfigInstructionsFile { Name = "lang-csharp", Disabled = true }],
        };
        var service = CreateService(BuildRegistry(), [CSharpFile()], config);

        var result = service.RouteForPrompt("fix my C# in Foo.cs");

        Assert.Multiple(
            () => Assert.Equal(["analyze_csharp_code"], result.Tools),
            () => Assert.Empty(result.Instructions));
    }

    [Fact]
    public void Should_route_a_tool_to_activation_flag_intersecting_files()
    {
        var service = CreateService(
            BuildRegistry(),
            [
                CSharpFile(),
                File("dotnet-testing", ["hasDotNet"]),
                File("lang-python", ["hasPython"]),
                File("code-review", []),
            ]);

        var result = service.RouteForTool("analyze_csharp_code");

        // Shares hasDotNet/hasCSharp with lang-csharp + dotnet-testing;
        // python and the flagless code-review file are excluded.
        Assert.Equal(
            ["lang-csharp.instructions.md", "dotnet-testing.instructions.md"],
            result.Instructions);
    }

    [Fact]
    public void Should_return_an_empty_tool_route_for_an_unknown_tool()
    {
        var service = CreateService(BuildRegistry(), [CSharpFile()]);

        Assert.Empty(service.RouteForTool("does_not_exist").Instructions);
    }

    [Fact]
    public void Should_return_an_empty_tool_route_for_a_flagless_tool()
    {
        var registry = new McpToolsRegistry(
            [Category("Workspace")],
            [Tool("read_editorconfig", "Workspace", activationFlags: [])]);
        var service = CreateService(registry, [CSharpFile()]);

        Assert.Empty(service.RouteForTool("read_editorconfig").Instructions);
    }

    [Fact]
    public void Should_exclude_a_disabled_file_from_a_tool_route()
    {
        var config = new ConfigSnapshot
        {
            Instructions = [new ConfigInstructionsFile { Name = "lang-csharp", Disabled = true }],
        };
        var service = CreateService(BuildRegistry(), [CSharpFile()], config);

        Assert.Empty(service.RouteForTool("analyze_csharp_code").Instructions);
    }

    [Fact]
    public void Should_reflect_a_disabled_state_change_without_an_index_rebuild()
    {
        var configAccessor = new FakeConfigSnapshotAccessor { Current = ConfigSnapshot.Empty };
        var service = new DiscoveryService(
            new FakeMcpToolsRegistryAccessor(BuildRegistry()),
            new FakeInstructionsManifestAccessor(CSharpFile()),
            configAccessor);

        Assert.Equal(["analyze_csharp_code"], service.RouteForPrompt("fix my C#").Tools);

        configAccessor.Current = new ConfigSnapshot
        {
            McpTools = [new ConfigMcpTool { Name = "analyze_csharp_code", Disabled = true }],
        };

        Assert.Empty(service.RouteForPrompt("fix my C#").Tools);
    }

    private static DiscoveryService CreateService(
        McpToolsRegistry registry,
        IReadOnlyList<InstructionsFileManifestEntry> files,
        ConfigSnapshot? config = null)
        => new(
            new FakeMcpToolsRegistryAccessor(registry),
            new FakeInstructionsManifestAccessor([.. files]),
            new FakeConfigSnapshotAccessor { Current = config ?? ConfigSnapshot.Empty });

    private static McpToolsRegistry BuildRegistry()
        => new(
            [Category(".NET"), Category("C#", parent: ".NET")],
            [Tool("analyze_csharp_code", "C#", activationFlags: ["hasDotNet", "hasCSharp"])]);

    private static InstructionsFileManifestEntry CSharpFile()
        => File("lang-csharp", ["hasDotNet", "hasCSharp"], extensions: ["cs"]);

    private static McpToolsCategoryEntry Category(string name, string? parent = null)
        => new() { Name = name, Description = name, Parent = parent };

    private static McpToolsRegistryEntry Tool(
        string name, string category, IReadOnlyList<string> activationFlags)
        => new()
        {
            Name = name,
            Category = category,
            WorkerId = "dotnet",
            ModelDescription = name,
            DisplayDescription = name,
            Parameters = [],
            ActivationFlags = activationFlags,
        };

    private static InstructionsFileManifestEntry File(
        string key,
        IReadOnlyList<string> activationFlags,
        IReadOnlyList<string>? extensions = null)
        => new()
        {
            Key = key,
            FileName = key + ".instructions.md",
            Name = key + " (v1.0.0)",
            Version = "1.0.0",
            Description = key,
            HasChangelog = false,
            ContentHash = "sha256:0",
            AlwaysAttached = false,
            Extensions = extensions,
            ActivationFlags = activationFlags,
        };
}
