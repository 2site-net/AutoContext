namespace AutoContext.Engine.Core.Workspace.Context;

using System.Text.RegularExpressions;

/// <summary>
/// A single content-scan rule: when the text of one of its enclosing
/// <see cref="ContentScan"/>'s selected files matches
/// <paramref name="Pattern"/>, the workspace flag named
/// <paramref name="Flag"/> is set. Pure data — the scan itself lives in
/// the detector. Case sensitivity is carried by the regex, not by the
/// type, so a new platform's rules need no new type.
/// </summary>
/// <param name="Flag">The workspace flag name this rule sets (e.g.
/// <c>hasReact</c>). Matches a property on the wire
/// <c>JsonWorkspaceFlags</c> contract.</param>
/// <param name="Pattern">The regular expression tested against the
/// selected files' contents.</param>
internal sealed record ContentPatternRule(string Flag, Regex Pattern);
