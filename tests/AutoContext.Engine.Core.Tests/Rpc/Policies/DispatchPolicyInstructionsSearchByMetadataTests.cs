namespace AutoContext.Engine.Core.Tests.Rpc.Policies;

using System.Linq;
using System.Text.Json;

using AutoContext.Engine.Core.Features.Instructions.Snapshot;
using AutoContext.Engine.Core.Rpc.Results;
using AutoContext.Engine.Core.Tests.Support;
using AutoContext.Engine.Core.Tests.Support.Features.Instructions;
using AutoContext.Engine.Core.Tests.Support.Rpc;
using AutoContext.Engine.Core.Tests.Support.Rpc.Policies;
using AutoContext.Engine.Core.Tests.Support.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;
using AutoContext.Engine.Protocol.Messages.Instructions;
using AutoContext.Engine.Protocol.Serialization;

public sealed class DispatchPolicyInstructionsSearchByMetadataTests
{
    [Fact]
    public async Task Should_return_the_files_matching_the_predicate()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var manifest = new FakeInstructionsManifestAccessor(
            InstructionsFileManifestEntryTestFactory.Create("lang-csharp", description: "C# code style"),
            InstructionsFileManifestEntryTestFactory.Create("lang-typescript", description: "TypeScript guide"));
        var policy = DispatchPolicyTestFactory.Create(lifetime, manifestAccessor: manifest);
        var request = JsonRpcRequestTestFactory.BuildRequest(
            InstructionsMethods.SearchByMetadata,
            new JsonInstructionsSearchByMetadataParams
            {
                Predicate = InstructionsMetadataPredicateTestFactory.Build(("description", "code style")),
            },
            ProtocolJsonContext.Default.JsonInstructionsSearchByMetadataParams);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var ok = Assert.IsType<JsonInstructionsSearchByMetadataOkResult>(
            JsonSerializer.Deserialize(
                result.Response.Result!.Value,
                ProtocolJsonContext.Default.JsonInstructionsSearchByMetadataResult));
        Assert.Equal(["lang-csharp"], ok.Results.Select(r => r.File.Key));
    }

    [Fact]
    public async Task Should_drop_disabled_files_from_the_results()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var manifest = new FakeInstructionsManifestAccessor(
            InstructionsFileManifestEntryTestFactory.Create("lang-csharp"),
            InstructionsFileManifestEntryTestFactory.Create("lang-typescript"));
        var config = new FakeConfigSnapshotAccessor
        {
            Current = ConfigSnapshot.Empty with
            {
                Instructions = [new ConfigInstructionsFile { Name = "lang-csharp", Disabled = true }],
            },
        };
        var policy = DispatchPolicyTestFactory.Create(
            lifetime, configAccessor: config, manifestAccessor: manifest);
        var request = JsonRpcRequestTestFactory.BuildRequest(
            InstructionsMethods.SearchByMetadata,
            new JsonInstructionsSearchByMetadataParams(),
            ProtocolJsonContext.Default.JsonInstructionsSearchByMetadataParams);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var ok = Assert.IsType<JsonInstructionsSearchByMetadataOkResult>(
            JsonSerializer.Deserialize(
                result.Response.Result!.Value,
                ProtocolJsonContext.Default.JsonInstructionsSearchByMetadataResult));
        Assert.Equal(["lang-typescript"], ok.Results.Select(r => r.File.Key));
    }

    [Fact]
    public async Task Should_return_an_error_envelope_for_an_unknown_field()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var manifest = new FakeInstructionsManifestAccessor(
            InstructionsFileManifestEntryTestFactory.Create("lang-csharp"));
        var policy = DispatchPolicyTestFactory.Create(lifetime, manifestAccessor: manifest);
        var request = JsonRpcRequestTestFactory.BuildRequest(
            InstructionsMethods.SearchByMetadata,
            new JsonInstructionsSearchByMetadataParams
            {
                Predicate = InstructionsMetadataPredicateTestFactory.Build(("bogus", "x")),
            },
            ProtocolJsonContext.Default.JsonInstructionsSearchByMetadataParams);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var error = Assert.IsType<JsonInstructionsSearchByMetadataErrorResult>(
            JsonSerializer.Deserialize(
                result.Response.Result!.Value,
                ProtocolJsonContext.Default.JsonInstructionsSearchByMetadataResult));
        Assert.Multiple(
            () => Assert.Equal("unknown-field", error.Error),
            () => Assert.Equal("bogus", error.Field),
            () => Assert.NotEmpty(error.RecognizedFields));
    }

    [Fact]
    public async Task Should_attach_matched_anchors_and_sections_for_a_sections_clause()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var manifest = new FakeInstructionsManifestAccessor(
            InstructionsFileManifestEntryTestFactory.Create(
                "lang-csharp",
                sections: [new InstructionsSection { Heading = "Naming", Anchor = "naming" }]));
        var policy = DispatchPolicyTestFactory.Create(lifetime, manifestAccessor: manifest);
        var request = JsonRpcRequestTestFactory.BuildRequest(
            InstructionsMethods.SearchByMetadata,
            new JsonInstructionsSearchByMetadataParams
            {
                Predicate = InstructionsMetadataPredicateTestFactory.Build(("sections.heading", "Naming")),
            },
            ProtocolJsonContext.Default.JsonInstructionsSearchByMetadataParams);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var ok = Assert.IsType<JsonInstructionsSearchByMetadataOkResult>(
            JsonSerializer.Deserialize(
                result.Response.Result!.Value,
                ProtocolJsonContext.Default.JsonInstructionsSearchByMetadataResult));
        Assert.Multiple(
            () => Assert.Equal(["naming"], ok.Results[0].MatchedAnchors),
            () => Assert.NotNull(ok.Results[0].File.Sections));
    }

    [Fact]
    public async Task Should_omit_sections_and_anchors_when_no_sections_clause_is_present()
    {
        // Arrange
        using var lifetime = new FakeHostApplicationLifetime();
        var manifest = new FakeInstructionsManifestAccessor(
            InstructionsFileManifestEntryTestFactory.Create(
                "lang-csharp",
                description: "C# code style",
                sections: [new InstructionsSection { Heading = "Naming", Anchor = "naming" }]));
        var policy = DispatchPolicyTestFactory.Create(lifetime, manifestAccessor: manifest);
        var request = JsonRpcRequestTestFactory.BuildRequest(
            InstructionsMethods.SearchByMetadata,
            new JsonInstructionsSearchByMetadataParams
            {
                Predicate = InstructionsMetadataPredicateTestFactory.Build(("description", "code")),
            },
            ProtocolJsonContext.Default.JsonInstructionsSearchByMetadataParams);

        // Act
        var result = Assert.IsType<UnaryHandlerResult>(
            await policy.InvokeAsync(request, TestContext.Current.CancellationToken));

        // Assert
        var ok = Assert.IsType<JsonInstructionsSearchByMetadataOkResult>(
            JsonSerializer.Deserialize(
                result.Response.Result!.Value,
                ProtocolJsonContext.Default.JsonInstructionsSearchByMetadataResult));
        Assert.Multiple(
            () => Assert.Null(ok.Results[0].MatchedAnchors),
            () => Assert.Null(ok.Results[0].File.Sections));
    }
}
