namespace AutoContext.Engine.Core.Workspace.Config.Snapshot;

/// <summary>
/// Immutable engine-only settings from the <c>engine</c> block of
/// <c>.autocontext.json</c>. Pure data carried through verbatim so the
/// engine never drops a user's settings when it rewrites the file.
/// </summary>
internal sealed record ConfigEngineSettings
{
    /// <summary>
    /// Workspace-relative directories, in precedence order, whose
    /// <c>instructions/</c> subfolder the engine watches for
    /// <c>*.instructions.md</c> overrides. Empty when the user never
    /// set it, in which case the engine applies its default.
    /// </summary>
    public IReadOnlyList<string> InstructionsOverrideRoots { get; init; } = [];
}
