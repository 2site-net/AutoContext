namespace AutoContext.Engine.Core.Tests.Features.Instructions;

using System.Collections.Frozen;

using AutoContext.Engine.Core.Features.Instructions;
using AutoContext.Engine.Core.Features.Instructions.Snapshot;
using AutoContext.Engine.Core.Tests.Support.Features.Instructions;
using AutoContext.Engine.Core.Tests.Support.Lifecycle;
using AutoContext.Engine.Core.Tests.Support.Workspace.Config;
using AutoContext.Engine.Core.Tests.Support.Workspace.Context;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;
using AutoContext.Engine.Core.Workspace.Context;
using AutoContext.Engine.Protocol.Messages.Instructions;

public sealed class InstructionsListProjectorTests
{
    [Fact]
    public void Should_throw_when_constructed_with_null_manifest_accessor()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new InstructionsListProjector(
                null!,
                LifecycleServiceFixture.CreateInstructionsOverridesAccessor(),
                LifecycleServiceFixture.CreateConfigAccessor(),
                LifecycleServiceFixture.CreateWorkspaceAccessor()));
    }

    [Fact]
    public void ProjectAll_should_return_a_row_per_manifest_file_with_sections()
    {
        // Arrange
        var manifest = new FakeInstructionsManifestAccessor(
            InstructionsFileManifestEntryTestFactory.Create(
                "testing",
                sections: [new InstructionsSection { Heading = "Alpha", Anchor = "alpha" }]),
            InstructionsFileManifestEntryTestFactory.Create("design"));
        var projector = LifecycleServiceFixture.CreateInstructionsListProjector(manifest: manifest);

        // Act
        var rows = projector.ProjectAll();

        // Assert — every file, sections included, no workspace filter.
        Assert.Multiple(
            () => Assert.Equal(2, rows.Count),
            () => Assert.Equal("testing", rows[0].Key),
            () => Assert.NotNull(rows[0].Sections),
            () => Assert.Equal("alpha", rows[0].Sections![0].Anchor),
            () => Assert.Equal(InstructionsSource.Bundled, rows[0].Source));
    }

    [Fact]
    public void ProjectAll_should_not_apply_workspace_extension_filter()
    {
        // Arrange — a TypeScript-only file in a C#-only workspace is
        // still listed in full, because the subscribe snapshot is the
        // whole corpus.
        var manifest = new FakeInstructionsManifestAccessor(
            InstructionsFileManifestEntryTestFactory.Create("tsonly", applyTo: "**/*.ts", extensions: ["ts"]));
        var workspace = new FakeWorkspaceContextAccessor
        {
            Current = new WorkspaceDetectionResult { Flags = FrozenSet<string>.Empty, Extensions = ["cs"] },
        };
        var projector = LifecycleServiceFixture.CreateInstructionsListProjector(
            manifest: manifest, workspace: workspace);

        // Act
        var rows = projector.ProjectAll();

        // Assert
        Assert.Equal("tsonly", Assert.Single(rows).Key);
    }

    [Fact]
    public void ProjectAll_should_mark_row_disabled_when_config_disables_file()
    {
        // Arrange
        var manifest = new FakeInstructionsManifestAccessor(
            InstructionsFileManifestEntryTestFactory.Create("testing"));
        var config = new FakeConfigSnapshotAccessor
        {
            Current = ConfigSnapshot.Empty with
            {
                Instructions = [new ConfigInstructionsFile { Name = "testing", Disabled = true }],
            },
        };
        var projector = LifecycleServiceFixture.CreateInstructionsListProjector(
            manifest: manifest, config: config);

        // Act
        var rows = projector.ProjectAll();

        // Assert
        Assert.True(Assert.Single(rows).Disabled);
    }
}
