namespace AutoContext.Engine.Core.Workspace.Context;

/// <summary>
/// How a single <see cref="FileSelector"/> value is matched against a
/// workspace file. Each selector carries exactly one criterion of one
/// kind; a presence rule fires, or a content scan reads a file, when any
/// of its selectors matches.
/// </summary>
internal enum FileSelectorKind
{
    /// <summary>
    /// Match by file extension, without the leading dot (e.g. <c>cs</c>
    /// matches <c>Program.cs</c>). The most common and cheapest selector.
    /// </summary>
    Extension,

    /// <summary>
    /// Match by exact file name including extension (e.g.
    /// <c>Cargo.toml</c>), in any directory.
    /// </summary>
    FileName,

    /// <summary>
    /// Match by glob pattern, for the few selectors that cannot be
    /// expressed as an extension or file name (e.g. <c>**/Dockerfile*</c>
    /// or a fixed nested path).
    /// </summary>
    GlobPattern,
}
