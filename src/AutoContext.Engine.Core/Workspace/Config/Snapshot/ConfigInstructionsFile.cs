namespace AutoContext.Engine.Core.Workspace.Config.Snapshot;

/// <summary>
/// Immutable state of a single instructions file from the
/// <c>instructions</c> section of <c>.autocontext.json</c>: whether the
/// whole file is disabled, the version its rule state was captured
/// against, and the individual rules turned off within it. Pure data.
/// </summary>
internal sealed record ConfigInstructionsFile
{
    /// <summary>
    /// <see langword="true"/> when the whole instructions file is
    /// disabled. <see langword="null"/> when enabled.
    /// </summary>
    public bool? Disabled { get; init; }

    /// <summary>
    /// The instructions file name this entry applies to.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The individual rules whose state is recorded for this file, in
    /// the order they appear on disk.
    /// </summary>
    public InstructionsRule[] Rules { get; init; } = [];

    /// <summary>
    /// The MAJOR.MINOR instructions-file version the disabled rules were
    /// recorded against. <see langword="null"/> when unset.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Immutable state of a single rule within an instructions file.
    /// Pure data.
    /// </summary>
    internal sealed record InstructionsRule
    {
        /// <summary>
        /// <see langword="true"/> when the rule is disabled.
        /// <see langword="null"/> when enabled.
        /// </summary>
        public bool? Disabled { get; init; }

        /// <summary>
        /// The rule id this entry applies to.
        /// </summary>
        public string? Id { get; init; }
    }
}
