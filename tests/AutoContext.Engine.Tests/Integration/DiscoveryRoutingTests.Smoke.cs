namespace AutoContext.Engine.Tests.Integration;

using System.Text.Json;

using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.Messages.Config;
using AutoContext.Engine.Protocol.Messages.Discovery;
using AutoContext.Engine.Protocol.Serialization;
using AutoContext.Engine.Tests.Support.Diagnostics;
using AutoContext.Engine.Tests.Support.IO;
using AutoContext.Engine.Tests.Support.Pipes;
using AutoContext.Framework.Pipes;

/// <summary>
/// End-to-end coverage for the <c>Discovery.*</c> routing family
/// (Phase 9). Spawns the <c>autocontext-engine</c> binary against a
/// fresh workspace and proves the prompt- and tool-routing contracts
/// over the <c>rpc</c> pipe against the engine's own bundled tool
/// catalog and instructions corpus: <c>Discovery.RouteForPrompt</c>
/// scans a prompt into matched categories, extensions, tools, and
/// instructions files; a prompt naming two unrelated domains unions
/// both their tools and files while excluding every unrelated one;
/// <c>Discovery.RouteForTool</c> bridges a tool to the several
/// instructions files whose activation flags it shares while excluding
/// the files it does not; a flagless or unknown tool yields an empty
/// route; and a <c>Config.ToggleFile</c> write drops the disabled file
/// from the very next prompt route without any index rebuild.
/// </summary>
/// <remarks>
/// <para>
/// Discovery reads indices the engine builds from its bundled
/// <c>mcp-tools-catalog.json</c> and instructions manifest, both loaded
/// during host start before the dispatcher accepts connections, so the
/// first route already reflects the full catalog. Routing is purely
/// prompt-driven — independent of the empty workspace's detected
/// technologies — so the assertions bind to the shipped catalog, not to
/// anything seeded on disk.
/// </para>
/// <para>
/// The disabled-filter arm exercises the per-query config read: the
/// service holds its structural indices immutable but consults the live
/// config snapshot on every call, so the <c>Config.ToggleFile</c> edit
/// (applied to the snapshot accessor before its response returns) is
/// visible to the next route on the same connection.
/// </para>
/// <para>
/// Gated with the repository's <c>Category=Smoke</c> trait so it runs
/// under <c>.\scripts\test.ps1 -Smoke DotNet</c> and stays out of the
/// default unit-test pass.
/// </para>
/// </remarks>
[Trait("Category", "Smoke")]
public sealed class DiscoveryRoutingTests
{
    private const string CSharpFile = "lang-csharp.instructions.md";
    private const string DotNetFile = "dotnet-coding-standards.instructions.md";
    private const string TypeScriptFile = "lang-typescript.instructions.md";
    private const string CSharpKey = "lang-csharp";
    private const string CSharpPrompt = "please port this helper to c# and tidy up Foo.cs";
    private const string MultiPrompt = "refactor the c# service and the typescript client in Foo.cs and app.ts";
    private const string CSharpCodeTool = "analyze_csharp_code_style";
    private const string CSharpStructureTool = "analyze_csharp_project_structure";
    private const string CSharpTestingTool = "analyze_csharp_testing_style";
    private const string TypeScriptTool = "analyze_typescript_code_style";
    private const string NuGetTool = "analyze_nuget_references";
    private const string GitTool = "analyze_git_commit_message";
    private const string FlaglessTool = "read_editorconfig_rules";
    private const string UnknownTool = "does_not_exist";

    [Fact]
    public async Task Should_route_prompts_and_tools_over_rpc_against_the_bundled_catalog()
    {
        // Arrange — a fresh workspace and cache root; Discovery answers
        // from the engine's bundled catalog + corpus, so nothing needs
        // seeding on disk.
        var ct = TestContext.Current.CancellationToken;
        using var cache = IsolatedCacheRoot.Create();
        using var workspace = WorkspaceTestDirectoryFactory.Create();

        await using var engine = new EngineTestProcess
        {
            Options = new()
            {
                WorkspacePath = workspace.Path,
                CacheRootOverride = cache.Path,
            },
        };
        await engine.SpawnAsync(ct);

        var rpc = await EngineWireTestClient.ConnectAsync(EndpointKind.Rpc, engine, ct);
        await using var rpcDisposer = rpc.ConfigureAwait(false);
        var codec = new LengthPrefixedFrameCodec(rpc);

        await EngineWireTestClient.SendHelloAsync(codec, ProtocolVersion.Current, ct);
        await EngineWireTestClient.ReadResponseAsync(codec, "Engine.Hello response", ct);

        // Act — route a single-domain C# prompt, route a two-domain
        // prompt (C# + TypeScript), bridge a C# tool, probe the
        // empty-route arms, then disable the C# file and re-route.
        var promptRoute = await RouteForPromptAsync(codec, id: 2, CSharpPrompt, ct);
        var multiRoute = await RouteForPromptAsync(codec, id: 3, MultiPrompt, ct);
        var toolRoute = await RouteForToolAsync(codec, id: 4, CSharpCodeTool, ct);
        var flaglessRoute = await RouteForToolAsync(codec, id: 5, FlaglessTool, ct);
        var unknownRoute = await RouteForToolAsync(codec, id: 6, UnknownTool, ct);

        await ToggleFileAsync(codec, id: 7, CSharpKey, ct);
        var promptRouteAfterDisable = await RouteForPromptAsync(codec, id: 8, CSharpPrompt, ct);

        // Assert
        Assert.Multiple(
            // Prompt routing: the "c#" word resolves to the C# category
            // and its three tools; the ".cs" extension resolves to the
            // C# instructions file.
            () => Assert.Contains("C#", promptRoute.MatchedCategories),
            () => Assert.Contains(".cs", promptRoute.MatchedExtensions),
            () => Assert.Contains(CSharpCodeTool, promptRoute.Tools),
            () => Assert.Contains(CSharpStructureTool, promptRoute.Tools),
            () => Assert.Contains(CSharpTestingTool, promptRoute.Tools),
            () => Assert.Contains(CSharpFile, promptRoute.Instructions),
            // Multi-domain prompt: both categories match and their tools
            // union, both extensions match and their files union, while
            // unrelated-domain tools (NuGet, Git, EditorConfig) stay out.
            () => Assert.Contains("C#", multiRoute.MatchedCategories),
            () => Assert.Contains("TypeScript", multiRoute.MatchedCategories),
            () => Assert.Contains(".cs", multiRoute.MatchedExtensions),
            () => Assert.Contains(".ts", multiRoute.MatchedExtensions),
            () => Assert.Contains(CSharpCodeTool, multiRoute.Tools),
            () => Assert.Contains(TypeScriptTool, multiRoute.Tools),
            () => Assert.DoesNotContain(NuGetTool, multiRoute.Tools),
            () => Assert.DoesNotContain(GitTool, multiRoute.Tools),
            () => Assert.DoesNotContain(FlaglessTool, multiRoute.Tools),
            () => Assert.Contains(CSharpFile, multiRoute.Instructions),
            () => Assert.Contains(TypeScriptFile, multiRoute.Instructions),
            // Tool routing: the C# analyzer shares flags with several
            // instructions files (its own plus every hasDotNet file), so
            // more than one surfaces; a TypeScript-only file does not.
            () => Assert.Contains(CSharpFile, toolRoute.Instructions),
            () => Assert.Contains(DotNetFile, toolRoute.Instructions),
            () => Assert.DoesNotContain(TypeScriptFile, toolRoute.Instructions),
            // A tool with no activation flags bridges to nothing.
            () => Assert.Empty(flaglessRoute.Instructions),
            // An unknown tool bridges to nothing.
            () => Assert.Empty(unknownRoute.Instructions),
            // Disabled filter: toggling the C# file off drops it from the
            // next prompt route, while other .cs-matched files remain.
            () => Assert.DoesNotContain(CSharpFile, promptRouteAfterDisable.Instructions),
            () => Assert.NotEmpty(promptRouteAfterDisable.Instructions));

        static async Task<JsonDiscoveryRouteForPromptResult> RouteForPromptAsync(
            LengthPrefixedFrameCodec codec, int id, string prompt, CancellationToken cancellationToken)
        {
            var parameters = JsonSerializer.SerializeToElement(
                new JsonDiscoveryRouteForPromptParams { Prompt = prompt },
                ProtocolJsonContext.Default.JsonDiscoveryRouteForPromptParams);
            await EngineWireTestClient.SendRequestAsync(
                codec, id, DiscoveryMethods.RouteForPrompt, parameters, cancellationToken);
            var response = await EngineWireTestClient.ReadResponseAsync(
                codec, "Discovery.RouteForPrompt response", cancellationToken);
            Assert.Null(response.Error);
            var result = response.Result!.Value.Deserialize(
                ProtocolJsonContext.Default.JsonDiscoveryRouteForPromptResult);
            Assert.NotNull(result);
            return result!;
        }

        static async Task<JsonDiscoveryRouteForToolResult> RouteForToolAsync(
            LengthPrefixedFrameCodec codec, int id, string name, CancellationToken cancellationToken)
        {
            var parameters = JsonSerializer.SerializeToElement(
                new JsonDiscoveryRouteForToolParams { Name = name },
                ProtocolJsonContext.Default.JsonDiscoveryRouteForToolParams);
            await EngineWireTestClient.SendRequestAsync(
                codec, id, DiscoveryMethods.RouteForTool, parameters, cancellationToken);
            var response = await EngineWireTestClient.ReadResponseAsync(
                codec, "Discovery.RouteForTool response", cancellationToken);
            Assert.Null(response.Error);
            var result = response.Result!.Value.Deserialize(
                ProtocolJsonContext.Default.JsonDiscoveryRouteForToolResult);
            Assert.NotNull(result);
            return result!;
        }

        static async Task ToggleFileAsync(
            LengthPrefixedFrameCodec codec, int id, string name, CancellationToken cancellationToken)
        {
            var parameters = JsonSerializer.SerializeToElement(
                new JsonConfigToggleFileParams { Name = name },
                ProtocolJsonContext.Default.JsonConfigToggleFileParams);
            await EngineWireTestClient.SendRequestAsync(
                codec, id, ConfigMethods.ToggleFile, parameters, cancellationToken);
            var response = await EngineWireTestClient.ReadResponseAsync(
                codec, "Config.ToggleFile response", cancellationToken);
            Assert.Null(response.Error);
        }
    }
}
