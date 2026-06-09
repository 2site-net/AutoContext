namespace AutoContext.Engine.Core.Tests.Support.Features.Instructions;

using AutoContext.Engine.Core.Features.Instructions;
using AutoContext.Engine.Core.Features.Instructions.Snapshot;

/// <summary>
/// In-memory <see cref="IInstructionsManifestAccessor"/> test double that
/// returns a fixed corpus snapshot, letting tests drive the content-search
/// index without loading the build-time catalog. The snapshot is settable so
/// tests can swap the corpus between reads.
/// </summary>
internal sealed class FakeInstructionsManifestAccessor : IInstructionsManifestAccessor
{
    public FakeInstructionsManifestAccessor(params InstructionsManifestFile[] files)
        => Current = new InstructionsManifestSnapshot([], files);

    public InstructionsManifestSnapshot Current { get; set; }
}
