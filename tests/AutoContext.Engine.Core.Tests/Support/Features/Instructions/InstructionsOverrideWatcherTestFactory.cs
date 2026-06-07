namespace AutoContext.Engine.Core.Tests.Support.Features.Instructions;

using AutoContext.Engine.Core.Features.Instructions;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Builds an <see cref="InstructionsOverrideWatcher"/> bound to a test
/// workspace directory with the engine's default debounce delay. The
/// override roots default to <c>.github</c> to mirror the engine's
/// resolved default.
/// </summary>
internal static class InstructionsOverrideWatcherTestFactory
{
    public static InstructionsOverrideWatcher Create(
        string workspacePath,
        TimeProvider? timeProvider = null,
        IReadOnlyList<string>? instructionsOverrideRoots = null)
        => new(
            workspacePath,
            instructionsOverrideRoots ?? [".github"],
            timeProvider ?? TimeProvider.System,
            InstructionsOverrideWatcher.DefaultDebounceDelay,
            NullLogger<InstructionsOverrideWatcher>.Instance);
}
