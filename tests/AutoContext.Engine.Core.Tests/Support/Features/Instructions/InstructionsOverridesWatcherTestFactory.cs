namespace AutoContext.Engine.Core.Tests.Support.Features.Instructions;

using AutoContext.Engine.Core.Features.Instructions;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Builds an <see cref="InstructionsOverridesWatcher"/> bound to a test
/// workspace directory with the engine's default debounce delay. The
/// override roots default to <c>.github</c> to mirror the engine's
/// resolved default.
/// </summary>
internal static class InstructionsOverridesWatcherTestFactory
{
    public static InstructionsOverridesWatcher Create(
        string workspacePath,
        TimeProvider? timeProvider = null,
        IReadOnlyList<string>? instructionsOverridesRoots = null)
        => new(
            workspacePath,
            instructionsOverridesRoots ?? [".github"],
            timeProvider ?? TimeProvider.System,
            InstructionsOverridesWatcher.DefaultDebounceDelay,
            NullLogger<InstructionsOverridesWatcher>.Instance);
}
