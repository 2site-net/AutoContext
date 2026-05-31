namespace AutoContext.Engine.Core.Workspace.Config;

using AutoContext.Engine.Core.Workspace.Config.Format;

/// <summary>
/// Immutable in-memory snapshot of a workspace's
/// <c>.autocontext.json</c>, modelled as a composed graph that mirrors
/// the file's structure: a top-level version, an optional diagnostic
/// block, and the per-instruction-file and per-MCP-tool entries. Pure
/// data with no behaviour — <see cref="AutoContextConfigManager"/> maps
/// it to and from the on-disk <see cref="JsonAutoContextConfig"/> wire
/// shape and owns the live snapshot.
/// </summary>
internal sealed record AutoContextConfig
{
    /// <summary>
    /// The shared empty snapshot: no version, no diagnostic block, and
    /// no entries.
    /// </summary>
    public static AutoContextConfig Empty { get; } = new();

    /// <summary>
    /// Optional diagnostic preferences, carried through verbatim.
    /// </summary>
    public DiagnosticConfig? Diagnostic { get; init; }

    /// <summary>
    /// Per-instruction-file entries, in the order they appear on disk.
    /// </summary>
    public InstructionsFileConfig[] Instructions { get; init; } = [];

    /// <summary>
    /// Per-MCP-tool entries, in the order they appear on disk.
    /// </summary>
    public McpToolConfig[] McpTools { get; init; } = [];

    /// <summary>
    /// Full semver of the engine build that last wrote the file.
    /// Informational; the manager re-stamps it on every save.
    /// </summary>
    public string? Version { get; init; }
}
