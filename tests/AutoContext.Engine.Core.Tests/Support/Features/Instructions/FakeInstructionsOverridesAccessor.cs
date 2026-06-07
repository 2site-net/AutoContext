namespace AutoContext.Engine.Core.Tests.Support.Features.Instructions;

using AutoContext.Engine.Core.Features.Instructions;
using AutoContext.Engine.Core.Features.Instructions.Snapshot;

/// <summary>
/// In-memory <see cref="IInstructionsOverridesAccessor"/> test double that
/// returns a fixed override inventory, letting tests drive
/// <see cref="InstructionsFileService"/> override resolution without a
/// stateful <see cref="InstructionsOverridesWatcher"/> or a file watcher.
/// </summary>
internal sealed class FakeInstructionsOverridesAccessor(InstructionsOverridesSnapshot? current = null)
    : IInstructionsOverridesAccessor
{
    public InstructionsOverridesSnapshot Current { get; } = current ?? InstructionsOverridesSnapshot.Empty;
}
