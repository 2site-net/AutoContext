namespace AutoContext.Engine.Core.Features.Instructions;

using AutoContext.Engine.Core.Workspace.Config.Snapshot;

/// <summary>
/// Resolves the workspace-relative directories the
/// <see cref="InstructionsOverrideWatcher"/> watches for instruction
/// overrides from a workspace's <c>.autocontext.json</c>. This is the
/// single place that knows the convention default (<c>.github</c>): the
/// watcher itself is directory-agnostic and watches whatever list it is
/// given.
/// </summary>
internal static class InstructionsOverrideRoots
{
    /// <summary>
    /// The default override roots applied when
    /// <c>engine."instructions.overrideRoots"</c> is absent or empty.
    /// Each entry is a directory whose <c>instructions/</c> subfolder
    /// holds the <c>*.instructions.md</c> overrides, so <c>.github</c>
    /// resolves to <c>.github/instructions/</c>.
    /// </summary>
    public static IReadOnlyList<string> Default { get; } = [".github"];

    /// <summary>
    /// Returns the configured override roots, falling back to
    /// <see cref="Default"/> when the workspace declares none.
    /// </summary>
    /// <param name="config">The workspace config snapshot. Must not be
    /// <see langword="null"/>.</param>
    /// <returns>The override roots, in precedence order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="config"/>
    /// is <see langword="null"/>.</exception>
    public static IReadOnlyList<string> Resolve(ConfigSnapshot config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return config.Engine?.InstructionsOverrideRoots is { Count: > 0 } configured
            ? configured
            : Default;
    }
}
