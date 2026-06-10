namespace AutoContext.Engine.Core.Tests.Support.Features.Instructions;

using AutoContext.Engine.Core.Features.Instructions;
using AutoContext.Engine.Core.Features.Instructions.Snapshot;
using AutoContext.Engine.Core.Workspace.Config;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Builds <see cref="InstructionsFullTextSearchService"/> instances over a
/// temp-directory corpus for tests, wiring the body projector, manifest
/// accessor, and config accessor the service composes so each test supplies
/// only the corpus files and workspace state it cares about.
/// </summary>
internal static class InstructionsFullTextSearchServiceTestFactory
{
    public static InstructionsFullTextSearchService Create(
        string instructionsDirectory,
        IConfigSnapshotAccessor config,
        IInstructionsOverridesAccessor overrides,
        params InstructionsFileManifestEntry[] files)
    {
        var projector = new InstructionsBodyProjector(instructionsDirectory, overrides, config);

        return new InstructionsFullTextSearchService(
            new FakeInstructionsManifestAccessor(files),
            projector,
            config,
            NullLogger<InstructionsFullTextSearchService>.Instance);
    }
}
