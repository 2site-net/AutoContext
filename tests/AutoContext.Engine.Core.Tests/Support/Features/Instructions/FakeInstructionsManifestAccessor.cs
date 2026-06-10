namespace AutoContext.Engine.Core.Tests.Support.Features.Instructions;

using AutoContext.Engine.Core.Features.Instructions;
using AutoContext.Engine.Core.Features.Instructions.Snapshot;

/// <summary>
/// In-memory <see cref="IInstructionsManifestAccessor"/> test double that
/// exposes a fixed corpus snapshot built from a set of manifest entries,
/// letting tests drive <see cref="InstructionsFullTextSearchService"/>
/// indexing without a stateful <see cref="InstructionsManifestService"/> or
/// the build-time side-cars.
/// </summary>
internal sealed class FakeInstructionsManifestAccessor(params InstructionsFileManifestEntry[] files)
    : IInstructionsManifestAccessor
{
    public FakeInstructionsManifestAccessor(
        IReadOnlyList<InstructionsCategory> categories, params InstructionsFileManifestEntry[] files)
        : this(files)
    {
        Current = new InstructionsManifestSnapshot(categories, files);
    }

    public InstructionsManifestSnapshot Current { get; } = new([], files);
}
