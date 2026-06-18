namespace AutoContext.Engine.Core.Tests.Rpc.Policies;

using System.Collections.Frozen;
using System.Text.Json;

using AutoContext.Engine.Core.Features.Instructions;
using AutoContext.Engine.Core.Features.Instructions.Snapshot;
using AutoContext.Engine.Core.Rpc.Results;
using AutoContext.Engine.Core.Tests.Support.Features.Instructions;
using AutoContext.Engine.Core.Tests.Support.Lifecycle;
using AutoContext.Engine.Core.Tests.Support.Rpc;
using AutoContext.Engine.Core.Tests.Support.Rpc.Policies;
using AutoContext.Engine.Core.Tests.Support.Workspace.Config;
using AutoContext.Engine.Core.Tests.Support.Workspace.Context;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;
using AutoContext.Engine.Core.Workspace.Context;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages.Instructions;
using AutoContext.Engine.Protocol.Serialization;
using AutoContext.Engine.Tests.Support.IO;

public sealed class DispatchPolicyInstructionsTests(TempDirectoryFixture tempDirectory)
    : IClassFixture<TempDirectoryFixture>
{
    private const string FileName = "testing.instructions.md";

    [Fact]
    public async Task Should_return_a_row_per_manifest_file_for_Instructions_List()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var manifest = new FakeInstructionsManifestAccessor(
            InstructionsFileManifestEntryTestFactory.Create(
                "testing",
                alwaysAttached: true,
                sections: [new InstructionsSection { Heading = "Alpha", Anchor = "alpha" }]),
            InstructionsFileManifestEntryTestFactory.Create("design", alwaysAttached: true));
        var policy = DispatchPolicyTestFactory.Create(lifetime, manifestAccessor: manifest);
        var request = JsonRpcRequestTestFactory.BuildRequest(InstructionsMethods.List);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var payload = JsonSerializer.Deserialize(
            result.Response.Result!.Value, ProtocolJsonContext.Default.JsonInstructionsListResult)!;
        Assert.Multiple(
            () => Assert.Equal(2, payload.Files.Count),
            () => Assert.Equal("testing", payload.Files[0].Key),
            () => Assert.NotNull(payload.Files[0].Sections),
            () => Assert.Equal("alpha", payload.Files[0].Sections![0].Anchor),
            () => Assert.Equal(InstructionsSource.Bundled, payload.Files[0].Source));
    }

    [Fact]
    public async Task Should_omit_sections_when_IncludeSections_is_false_for_Instructions_List()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var manifest = new FakeInstructionsManifestAccessor(
            InstructionsFileManifestEntryTestFactory.Create(
                "testing",
                alwaysAttached: true,
                sections: [new InstructionsSection { Heading = "Alpha", Anchor = "alpha" }]));
        var policy = DispatchPolicyTestFactory.Create(lifetime, manifestAccessor: manifest);
        var request = JsonRpcRequestTestFactory.BuildRequest(
            InstructionsMethods.List,
            new JsonInstructionsListParams { IncludeSections = false },
            ProtocolJsonContext.Default.JsonInstructionsListParams);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var payload = JsonSerializer.Deserialize(
            result.Response.Result!.Value, ProtocolJsonContext.Default.JsonInstructionsListResult)!;
        Assert.Null(payload.Files[0].Sections);
    }

    [Fact]
    public async Task Should_mark_row_disabled_when_config_disables_file_for_Instructions_List()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var manifest = new FakeInstructionsManifestAccessor(
            InstructionsFileManifestEntryTestFactory.Create("testing", alwaysAttached: true));
        var config = new FakeConfigSnapshotAccessor
        {
            Current = ConfigSnapshot.Empty with
            {
                Instructions = [new ConfigInstructionsFile { Name = "testing", Disabled = true }],
            },
        };
        var policy = DispatchPolicyTestFactory.Create(
            lifetime, configAccessor: config, manifestAccessor: manifest);
        var request = JsonRpcRequestTestFactory.BuildRequest(InstructionsMethods.List);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var payload = JsonSerializer.Deserialize(
            result.Response.Result!.Value, ProtocolJsonContext.Default.JsonInstructionsListResult)!;
        Assert.True(payload.Files[0].Disabled);
    }

    [Fact]
    public async Task Should_report_override_source_and_path_for_Instructions_List()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var directory = tempDirectory.CreateDirectory();
        var overridePath = InstructionsBodyTestFiles.Write(directory, FileName, InstructionsBodyTestFiles.Body);
        var overrides = new FakeInstructionsOverridesAccessor(
            new InstructionsOverridesSnapshot(new Dictionary<string, string> { [FileName] = overridePath }));
        var manifest = new FakeInstructionsManifestAccessor(
            InstructionsFileManifestEntryTestFactory.Create("testing", alwaysAttached: true));
        var policy = DispatchPolicyTestFactory.Create(
            lifetime, manifestAccessor: manifest, overridesAccessor: overrides);
        var request = JsonRpcRequestTestFactory.BuildRequest(InstructionsMethods.List);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var payload = JsonSerializer.Deserialize(
            result.Response.Result!.Value, ProtocolJsonContext.Default.JsonInstructionsListResult)!;
        Assert.Multiple(
            () => Assert.Equal(InstructionsSource.Override, payload.Files[0].Source),
            () => Assert.NotNull(payload.Files[0].OverridePath),
            () => Assert.EndsWith(FileName, payload.Files[0].OverridePath!, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_drop_rows_disjoint_from_workspace_extensions_for_Instructions_List()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var manifest = new FakeInstructionsManifestAccessor(
            InstructionsFileManifestEntryTestFactory.Create("always", alwaysAttached: true),
            InstructionsFileManifestEntryTestFactory.Create("tsonly", applyTo: "**/*.ts", extensions: ["ts"]));
        var workspace = new FakeWorkspaceContextAccessor
        {
            Current = new WorkspaceDetectionResult { Flags = FrozenSet<string>.Empty, Extensions = ["cs"] },
        };
        var policy = DispatchPolicyTestFactory.Create(
            lifetime, manifestAccessor: manifest, workspaceAccessor: workspace);
        var request = JsonRpcRequestTestFactory.BuildRequest(InstructionsMethods.List);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var payload = JsonSerializer.Deserialize(
            result.Response.Result!.Value, ProtocolJsonContext.Default.JsonInstructionsListResult)!;
        Assert.Multiple(
            () => Assert.Single(payload.Files),
            () => Assert.Equal("always", payload.Files[0].Key));
    }

    [Fact]
    public async Task Should_narrow_rows_to_ApplyToHint_extensions_for_Instructions_List()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var manifest = new FakeInstructionsManifestAccessor(
            InstructionsFileManifestEntryTestFactory.Create("tsonly", applyTo: "**/*.ts", extensions: ["ts"]),
            InstructionsFileManifestEntryTestFactory.Create("csonly", applyTo: "**/*.cs", extensions: ["cs"]));
        var policy = DispatchPolicyTestFactory.Create(lifetime, manifestAccessor: manifest);
        var request = JsonRpcRequestTestFactory.BuildRequest(
            InstructionsMethods.List,
            new JsonInstructionsListParams { ApplyToWorkspaceFilter = false, ApplyToHint = ".ts" },
            ProtocolJsonContext.Default.JsonInstructionsListParams);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var payload = JsonSerializer.Deserialize(
            result.Response.Result!.Value, ProtocolJsonContext.Default.JsonInstructionsListResult)!;
        Assert.Multiple(
            () => Assert.Single(payload.Files),
            () => Assert.Equal("tsonly", payload.Files[0].Key));
    }

    [Fact]
    public async Task Should_return_catalog_categories_for_Instructions_Categories()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var manifest = new FakeInstructionsManifestAccessor(
            [new InstructionsCategoryEntry { Name = "languages", Description = "Per-language rules." }]);
        var policy = DispatchPolicyTestFactory.Create(lifetime, manifestAccessor: manifest);
        var request = JsonRpcRequestTestFactory.BuildRequest(InstructionsMethods.Categories);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var payload = JsonSerializer.Deserialize(
            result.Response.Result!.Value, ProtocolJsonContext.Default.JsonInstructionsCategoriesResult)!;
        Assert.Multiple(
            () => Assert.Single(payload.Categories),
            () => Assert.Equal("languages", payload.Categories[0].Name),
            () => Assert.Equal("Per-language rules.", payload.Categories[0].Description));
    }

    [Fact]
    public async Task Should_return_ok_with_projected_body_for_Instructions_Get()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var directory = tempDirectory.CreateDirectory();
        InstructionsBodyTestFiles.Write(directory, FileName, InstructionsBodyTestFiles.Body);
        var overrides = new FakeInstructionsOverridesAccessor();
        var config = new FakeConfigSnapshotAccessor();
        var manifest = new FakeInstructionsManifestAccessor(
            InstructionsFileManifestEntryTestFactory.Create("testing"));
        var projector = new InstructionsBodyProjector(directory, overrides, config);
        var policy = DispatchPolicyTestFactory.Create(
            lifetime,
            configAccessor: config,
            manifestAccessor: manifest,
            overridesAccessor: overrides,
            bodyProjector: projector);
        var request = JsonRpcRequestTestFactory.BuildRequest(
            InstructionsMethods.Get,
            new JsonInstructionsGetParams { Name = "testing" },
            ProtocolJsonContext.Default.JsonInstructionsGetParams);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var payload = JsonSerializer.Deserialize(
            result.Response.Result!.Value, ProtocolJsonContext.Default.JsonInstructionsGetResult)!;
        var ok = Assert.IsType<JsonInstructionsGetOkResult>(payload);
        Assert.Multiple(
            () => Assert.Equal("testing", ok.Key),
            () => Assert.Contains("Alpha body line.", ok.Content, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Should_slice_to_requested_sections_for_Instructions_Get()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var directory = tempDirectory.CreateDirectory();
        InstructionsBodyTestFiles.Write(directory, FileName, InstructionsBodyTestFiles.Body);
        var overrides = new FakeInstructionsOverridesAccessor();
        var config = new FakeConfigSnapshotAccessor();
        var manifest = new FakeInstructionsManifestAccessor(
            InstructionsFileManifestEntryTestFactory.Create("testing"));
        var projector = new InstructionsBodyProjector(directory, overrides, config);
        var policy = DispatchPolicyTestFactory.Create(
            lifetime,
            configAccessor: config,
            manifestAccessor: manifest,
            overridesAccessor: overrides,
            bodyProjector: projector);
        var request = JsonRpcRequestTestFactory.BuildRequest(
            InstructionsMethods.Get,
            new JsonInstructionsGetParams { Name = "testing", Sections = ["alpha", "ghost"] },
            ProtocolJsonContext.Default.JsonInstructionsGetParams);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var payload = JsonSerializer.Deserialize(
            result.Response.Result!.Value, ProtocolJsonContext.Default.JsonInstructionsGetResult)!;
        var ok = Assert.IsType<JsonInstructionsGetOkResult>(payload);
        Assert.Multiple(
            () => Assert.Contains("alpha", ok.ReturnedSections),
            () => Assert.DoesNotContain("Beta body line.", ok.Content, StringComparison.Ordinal),
            () => Assert.NotNull(ok.NotFoundSections),
            () => Assert.Contains("ghost", ok.NotFoundSections!));
    }

    [Fact]
    public async Task Should_return_disabled_when_file_is_disabled_for_Instructions_Get()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var manifest = new FakeInstructionsManifestAccessor(
            InstructionsFileManifestEntryTestFactory.Create("testing"));
        var config = new FakeConfigSnapshotAccessor
        {
            Current = ConfigSnapshot.Empty with
            {
                Instructions = [new ConfigInstructionsFile { Name = "testing", Disabled = true }],
            },
        };
        var policy = DispatchPolicyTestFactory.Create(
            lifetime, configAccessor: config, manifestAccessor: manifest);
        var request = JsonRpcRequestTestFactory.BuildRequest(
            InstructionsMethods.Get,
            new JsonInstructionsGetParams { Name = "testing" },
            ProtocolJsonContext.Default.JsonInstructionsGetParams);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var payload = JsonSerializer.Deserialize(
            result.Response.Result!.Value, ProtocolJsonContext.Default.JsonInstructionsGetResult)!;
        var disabled = Assert.IsType<JsonInstructionsGetDisabledResult>(payload);
        Assert.Equal("testing", disabled.Key);
    }

    [Fact]
    public async Task Should_return_not_found_for_unknown_name_for_Instructions_Get()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = DispatchPolicyTestFactory.Create(lifetime);
        var request = JsonRpcRequestTestFactory.BuildRequest(
            InstructionsMethods.Get,
            new JsonInstructionsGetParams { Name = "ghost" },
            ProtocolJsonContext.Default.JsonInstructionsGetParams);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var payload = JsonSerializer.Deserialize(
            result.Response.Result!.Value, ProtocolJsonContext.Default.JsonInstructionsGetResult)!;
        var notFound = Assert.IsType<JsonInstructionsGetNotFoundResult>(payload);
        Assert.Equal("ghost", notFound.Name);
    }

    [Fact]
    public async Task Should_return_InvalidParams_when_name_missing_for_Instructions_Get()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = DispatchPolicyTestFactory.Create(lifetime);
        var request = JsonRpcRequestTestFactory.BuildRequest(
            InstructionsMethods.Get,
            new JsonInstructionsGetParams { Name = null },
            ProtocolJsonContext.Default.JsonInstructionsGetParams);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        Assert.Multiple(
            () => Assert.NotNull(result.Response.Error),
            () => Assert.Equal(JsonRpcErrorCodes.InvalidParams, result.Response.Error!.Code));
    }

    [Fact]
    public async Task Should_return_every_enabled_file_for_Instructions_GetAll()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var directory = tempDirectory.CreateDirectory();
        InstructionsBodyTestFiles.Write(directory, "testing.instructions.md", InstructionsBodyTestFiles.Body);
        InstructionsBodyTestFiles.Write(directory, "design.instructions.md", InstructionsBodyTestFiles.Body);
        var overrides = new FakeInstructionsOverridesAccessor();
        var config = new FakeConfigSnapshotAccessor
        {
            Current = ConfigSnapshot.Empty with
            {
                Instructions = [new ConfigInstructionsFile { Name = "design", Disabled = true }],
            },
        };
        var manifest = new FakeInstructionsManifestAccessor(
            InstructionsFileManifestEntryTestFactory.Create("testing"),
            InstructionsFileManifestEntryTestFactory.Create("design"));
        var projector = new InstructionsBodyProjector(directory, overrides, config);
        var policy = DispatchPolicyTestFactory.Create(
            lifetime,
            configAccessor: config,
            manifestAccessor: manifest,
            overridesAccessor: overrides,
            bodyProjector: projector);
        var request = JsonRpcRequestTestFactory.BuildRequest(InstructionsMethods.GetAll);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var payload = JsonSerializer.Deserialize(
            result.Response.Result!.Value, ProtocolJsonContext.Default.JsonInstructionsFilesResult)!;
        Assert.Multiple(
            () => Assert.Single(payload.Files),
            () => Assert.Equal("testing", payload.Files[0].Key));
    }

    [Fact]
    public async Task Should_return_only_always_attached_files_for_Instructions_GetAlwaysAttached()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var directory = tempDirectory.CreateDirectory();
        InstructionsBodyTestFiles.Write(directory, "copilot.instructions.md", InstructionsBodyTestFiles.Body);
        InstructionsBodyTestFiles.Write(directory, "testing.instructions.md", InstructionsBodyTestFiles.Body);
        var overrides = new FakeInstructionsOverridesAccessor();
        var config = new FakeConfigSnapshotAccessor();
        var manifest = new FakeInstructionsManifestAccessor(
            InstructionsFileManifestEntryTestFactory.Create("copilot", alwaysAttached: true),
            InstructionsFileManifestEntryTestFactory.Create("testing"));
        var projector = new InstructionsBodyProjector(directory, overrides, config);
        var policy = DispatchPolicyTestFactory.Create(
            lifetime,
            configAccessor: config,
            manifestAccessor: manifest,
            overridesAccessor: overrides,
            bodyProjector: projector);
        var request = JsonRpcRequestTestFactory.BuildRequest(InstructionsMethods.GetAlwaysAttached);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var payload = JsonSerializer.Deserialize(
            result.Response.Result!.Value, ProtocolJsonContext.Default.JsonInstructionsFilesResult)!;
        Assert.Multiple(
            () => Assert.Single(payload.Files),
            () => Assert.Equal("copilot", payload.Files[0].Key));
    }

    [Fact]
    public async Task Should_return_bundled_content_for_Instructions_GetRaw()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var directory = tempDirectory.CreateDirectory();
        InstructionsBodyTestFiles.Write(directory, FileName, InstructionsBodyTestFiles.Body);
        var overrides = new FakeInstructionsOverridesAccessor();
        var config = new FakeConfigSnapshotAccessor();
        var manifest = new FakeInstructionsManifestAccessor(
            InstructionsFileManifestEntryTestFactory.Create("testing"));
        var reader = new InstructionsFileReader(directory, overrides);
        var policy = DispatchPolicyTestFactory.Create(
            lifetime,
            configAccessor: config,
            manifestAccessor: manifest,
            overridesAccessor: overrides,
            fileReader: reader);
        var request = JsonRpcRequestTestFactory.BuildRequest(
            InstructionsMethods.GetRaw,
            new JsonInstructionsGetRawParams { Name = "testing", Source = InstructionsRawSource.Bundled },
            ProtocolJsonContext.Default.JsonInstructionsGetRawParams);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var payload = JsonSerializer.Deserialize(
            result.Response.Result!.Value, ProtocolJsonContext.Default.JsonInstructionsGetRawResult)!;
        var ok = Assert.IsType<JsonInstructionsGetRawOkResult>(payload);
        Assert.Multiple(
            () => Assert.Equal(InstructionsSource.Bundled, ok.Source),
            () => Assert.Equal(InstructionsBodyTestFiles.Body, ok.Content));
    }

    [Fact]
    public async Task Should_prefer_override_for_active_source_for_Instructions_GetRaw()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var bundledDir = tempDirectory.CreateDirectory();
        InstructionsBodyTestFiles.Write(bundledDir, FileName, "bundled body");
        var overrideDir = tempDirectory.CreateDirectory();
        var overridePath = InstructionsBodyTestFiles.Write(overrideDir, FileName, "override body");
        var overrides = new FakeInstructionsOverridesAccessor(
            new InstructionsOverridesSnapshot(new Dictionary<string, string> { [FileName] = overridePath }));
        var config = new FakeConfigSnapshotAccessor();
        var manifest = new FakeInstructionsManifestAccessor(
            InstructionsFileManifestEntryTestFactory.Create("testing"));
        var reader = new InstructionsFileReader(bundledDir, overrides);
        var policy = DispatchPolicyTestFactory.Create(
            lifetime,
            configAccessor: config,
            manifestAccessor: manifest,
            overridesAccessor: overrides,
            fileReader: reader);
        var request = JsonRpcRequestTestFactory.BuildRequest(
            InstructionsMethods.GetRaw,
            new JsonInstructionsGetRawParams { Name = "testing", Source = InstructionsRawSource.Active },
            ProtocolJsonContext.Default.JsonInstructionsGetRawParams);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var payload = JsonSerializer.Deserialize(
            result.Response.Result!.Value, ProtocolJsonContext.Default.JsonInstructionsGetRawResult)!;
        var ok = Assert.IsType<JsonInstructionsGetRawOkResult>(payload);
        Assert.Multiple(
            () => Assert.Equal(InstructionsSource.Override, ok.Source),
            () => Assert.Equal("override body", ok.Content));
    }

    [Fact]
    public async Task Should_return_not_found_for_unknown_name_for_Instructions_GetRaw()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = DispatchPolicyTestFactory.Create(lifetime);
        var request = JsonRpcRequestTestFactory.BuildRequest(
            InstructionsMethods.GetRaw,
            new JsonInstructionsGetRawParams { Name = "ghost", Source = InstructionsRawSource.Bundled },
            ProtocolJsonContext.Default.JsonInstructionsGetRawParams);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var payload = JsonSerializer.Deserialize(
            result.Response.Result!.Value, ProtocolJsonContext.Default.JsonInstructionsGetRawResult)!;
        var notFound = Assert.IsType<JsonInstructionsGetRawNotFoundResult>(payload);
        Assert.Equal("ghost", notFound.Name);
    }

    [Fact]
    public async Task Should_return_ranked_hits_for_Instructions_SearchContent()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var directory = tempDirectory.CreateDirectory();
        InstructionsBodyTestFiles.Write(directory, FileName, InstructionsBodyTestFiles.Body);
        var overrides = new FakeInstructionsOverridesAccessor();
        var config = new FakeConfigSnapshotAccessor();
        var entry = InstructionsFileManifestEntryTestFactory.Create("testing");
        using var search = InstructionsFullTextSearchServiceTestFactory.Create(
            directory, config, overrides, entry);
        var policy = DispatchPolicyTestFactory.Create(
            lifetime,
            configAccessor: config,
            manifestAccessor: new FakeInstructionsManifestAccessor(entry),
            overridesAccessor: overrides,
            searchService: search);
        var request = JsonRpcRequestTestFactory.BuildRequest(
            InstructionsMethods.SearchContent,
            new JsonInstructionsSearchContentParams { Query = "Alpha" },
            ProtocolJsonContext.Default.JsonInstructionsSearchContentParams);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var payload = JsonSerializer.Deserialize(
            result.Response.Result!.Value, ProtocolJsonContext.Default.JsonInstructionsSearchContentResult)!;
        Assert.Multiple(
            () => Assert.Single(payload.Hits),
            () => Assert.Equal("testing", payload.Hits[0].Key),
            () => Assert.NotEmpty(payload.Hits[0].Excerpts));
    }

    [Fact]
    public async Task Should_return_InvalidParams_when_query_missing_for_Instructions_SearchContent()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var policy = DispatchPolicyTestFactory.Create(lifetime);
        var request = JsonRpcRequestTestFactory.BuildRequest(
            InstructionsMethods.SearchContent,
            new JsonInstructionsSearchContentParams { Query = "   " },
            ProtocolJsonContext.Default.JsonInstructionsSearchContentParams);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        Assert.Multiple(
            () => Assert.NotNull(result.Response.Error),
            () => Assert.Equal(JsonRpcErrorCodes.InvalidParams, result.Response.Error!.Code));
    }
}
