namespace AutoContext.Engine.Core.Workspace.Context;

/// <summary>
/// A single criterion that selects workspace files: a file extension, an
/// exact file name, or a glob pattern. Shared by both
/// <see cref="FilePresenceRule"/> (which fires a flag when any selected
/// file exists) and <see cref="ContentScan"/> (which scans the bodies of
/// the selected files). Pure data — the selection itself lives in the
/// detector.
/// </summary>
/// <param name="Value">The match value, interpreted per
/// <paramref name="Kind"/>: an extension without the leading dot, an
/// exact file name, or a glob pattern.</param>
/// <param name="Kind">How <paramref name="Value"/> is matched against a
/// workspace file.</param>
internal sealed record FileSelector(string Value, FileSelectorKind Kind);
