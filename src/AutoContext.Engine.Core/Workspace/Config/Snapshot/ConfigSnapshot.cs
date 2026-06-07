namespace AutoContext.Engine.Core.Workspace.Config.Snapshot;

using AutoContext.Engine.Core.Workspace.Config.Format;

/// <summary>
/// Immutable in-memory snapshot of a workspace's
/// <c>.autocontext.json</c>, modelled as a composed graph that mirrors
/// the file's structure: a top-level version, an optional diagnostic
/// block, and the per-instruction-file and per-MCP-tool entries. Pure
/// data with no behaviour — <see cref="ConfigFileManager"/> maps
/// it to and from the on-disk <see cref="JsonConfigFile"/> wire
/// shape and owns the live snapshot.
/// </summary>
internal sealed record ConfigSnapshot
{
    /// <summary>
    /// The shared empty snapshot: no version, no diagnostic block, and
    /// no entries.
    /// </summary>
    public static ConfigSnapshot Empty { get; } = new();

    /// <summary>
    /// Optional diagnostic preferences, carried through verbatim.
    /// </summary>
    public ConfigDiagnostic? Diagnostic { get; init; }

    /// <summary>
    /// Optional engine-only settings, carried through verbatim.
    /// </summary>
    public ConfigEngineSettings? Engine { get; init; }

    /// <summary>
    /// Per-instruction-file entries, in the order they appear on disk.
    /// </summary>
    public ConfigInstructionsFile[] Instructions { get; init; } = [];

    /// <summary>
    /// Per-MCP-tool entries, in the order they appear on disk.
    /// </summary>
    public ConfigMcpTool[] McpTools { get; init; } = [];

    /// <summary>
    /// Full semver of the engine build that last wrote the file.
    /// Informational; the manager re-stamps it on every save.
    /// </summary>
    public string? Version { get; init; }
}
