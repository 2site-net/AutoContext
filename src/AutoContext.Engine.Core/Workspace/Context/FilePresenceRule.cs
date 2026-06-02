namespace AutoContext.Engine.Core.Workspace.Context;

/// <summary>
/// A single file-presence detection rule: when any workspace file
/// matches one of <paramref name="Selectors"/>, the workspace flag named
/// <paramref name="Flag"/> is set. Presence only — this rule never reads
/// the file body (that is <see cref="ContentScan"/>'s job). Pure data —
/// the matching itself lives in the detector.
/// </summary>
/// <param name="Flag">The workspace flag name this rule sets (e.g.
/// <c>hasCSharp</c>). Matches a property on the wire
/// <c>JsonWorkspaceFlags</c> contract.</param>
/// <param name="Selectors">The criteria whose presence activates
/// <paramref name="Flag"/>. Each selector carries one
/// <see cref="FileSelectorKind"/> criterion; a single match on any
/// selector is enough (pure OR).</param>
internal sealed record FilePresenceRule(
    string Flag,
    IReadOnlyList<FileSelector> Selectors);
