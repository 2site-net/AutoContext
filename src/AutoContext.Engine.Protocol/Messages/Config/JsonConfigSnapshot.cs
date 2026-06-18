namespace AutoContext.Engine.Protocol.Messages.Config;

using System.Text.Json.Serialization;

/// <summary>
/// Wire shape of the engine's current config snapshot, returned by
/// the <c>Config.Get</c> RPC. Mirrors the engine-internal config
/// graph as flat arrays — instructions files and MCP tools each
/// carry their own enable/disable state plus per-file rule overrides
/// — rather than the
/// <c>.autocontext.json</c> on-disk encoding (which collapses
/// "no state" entries and uses a <c>false</c>-or-object shorthand).
/// Clients read disabled state off this shape; they never write the
/// file (the engine is the only writer — P-config-authority).
/// </summary>
public sealed record JsonConfigSnapshot
{
    /// <summary>
    /// Engine version stamped into the config on its last write, or
    /// <see langword="null"/> when the workspace has no config yet.
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    /// <summary>
    /// Diagnostic toggles, or <see langword="null"/> when none are set.
    /// </summary>
    [JsonPropertyName("diagnostic")]
    public JsonConfigDiagnostic? Diagnostic { get; init; }

    /// <summary>
    /// Per-instructions-file state. Empty when no instructions file
    /// carries an override.
    /// </summary>
    [JsonPropertyName("instructions")]
    public IReadOnlyList<JsonConfigInstructionsFile> Instructions { get; init; } = [];

    /// <summary>
    /// Per-MCP-tool state. Empty when no tool carries an override.
    /// </summary>
    [JsonPropertyName("mcpTools")]
    public IReadOnlyList<JsonConfigMcpTool> McpTools { get; init; } = [];
}
