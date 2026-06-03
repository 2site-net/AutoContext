namespace AutoContext.Engine.Core.Workspace.Context;

/// <summary>
/// A content-scan group: a set of manifest <paramref name="Selectors"/>
/// paired with the <paramref name="Rules"/> whose patterns are tested
/// against the bodies of the selected files. Grouping the selectors with
/// their rules mirrors the detector's read-each-manifest-once loop, and
/// makes a new platform a data edit — one more <see cref="ContentScan"/>
/// row — rather than a new type. Pure data; the scan lives in the
/// detector.
/// </summary>
/// <param name="Selectors">The files whose contents this scan reads
/// (e.g. <c>package.json</c> by name, or <c>.csproj</c>/<c>.fsproj</c>/
/// <c>.vbproj</c> by extension). A file is scanned if any selector
/// matches it.</param>
/// <param name="Rules">The pattern rules tested against the body of every
/// selected file; each match sets the rule's flag.</param>
internal sealed record ContentScan(
    IReadOnlyList<FileSelector> Selectors,
    IReadOnlyList<ContentPatternRule> Rules);
