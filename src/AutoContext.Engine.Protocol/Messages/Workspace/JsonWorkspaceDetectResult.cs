namespace AutoContext.Engine.Protocol.Messages.Workspace;

using System.Text.Json.Serialization;

/// <summary>
/// Wire shape returned by the <c>Workspace.Detect</c> RPC — the
/// engine's read-only description of the technologies present in the
/// pinned workspace. Carries the raised technology
/// <see cref="Flags"/> plus the <see cref="Extensions"/> index
/// derived from the same file-detection rules. Stateless and
/// idempotent: it describes workspace contents, so it carries no
/// engine-state revision counter (that lives on
/// <see cref="JsonWorkspaceInfoResult"/>). The override inventory is
/// reachable separately via <c>Instructions.List</c>. See
/// <c>design § RPC surface</c>.
/// </summary>
public sealed record JsonWorkspaceDetectResult
{
    /// <summary>
    /// The distinct file extensions (e.g. <c>cs</c>, <c>ts</c>,
    /// <c>py</c>) derived from the file-detection rule globs that
    /// matched, sorted and de-duplicated. Empty when no file rule
    /// fired.
    /// </summary>
    [JsonPropertyName("extensions")]
    public IReadOnlyList<string> Extensions { get; init; } = [];

    /// <summary>
    /// The full technology flag set. Every flag the engine
    /// positively detected is <see langword="true"/>; the rest
    /// default to <see langword="false"/>.
    /// </summary>
    [JsonPropertyName("flags")]
    public JsonWorkspaceFlags Flags { get; init; } = new();
}
