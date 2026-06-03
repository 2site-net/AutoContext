namespace AutoContext.Engine.Core.Workspace.Context;

using System.Collections.Frozen;
using System.IO.Enumeration;

/// <summary>
/// Streams every file under a workspace root in a single recursive
/// pass, pruning excluded directories during native recursion and
/// skipping individual entries that fail rather than aborting the
/// directory. Each directory is opened once — files and subdirectories
/// are reported from the same scan — and the excluded-directory test
/// runs against the <see cref="FileSystemEntry.FileName"/> span, so no
/// per-entry path string is allocated to decide recursion. The walk is
/// resilient: <see cref="ContinueOnError"/> skips a single faulting
/// entry rather than aborting the rest of a directory, which matters for
/// a detector pointed at a workspace being actively edited.
/// </summary>
/// <param name="directory">Absolute path of the workspace root to
/// walk.</param>
internal sealed class WorkspaceFileEnumerator(string directory)
    : FileSystemEnumerator<string>(directory, WalkOptions)
{
    private static readonly FrozenSet<string> ExcludedDirectories =
        new[] { "node_modules", "bin", "obj", ".git" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> ExcludedDirectoriesLookup =
        ExcludedDirectories.GetAlternateLookup<ReadOnlySpan<char>>();

    private static readonly EnumerationOptions WalkOptions = new()
    {
        IgnoreInaccessible = true,
        AttributesToSkip = 0,
        RecurseSubdirectories = true,
    };

    /// <summary>
    /// Reports whether <paramref name="name"/> is a directory whose
    /// subtree is pruned from the workspace walk (<c>node_modules</c>,
    /// <c>bin</c>, <c>obj</c>, <c>.git</c>). The single source of truth
    /// for the exclusion set, shared with the detector's watcher-path
    /// filter so the walk surface and the watch surface can never drift.
    /// </summary>
    /// <param name="name">A single path segment (directory name) to
    /// test, compared case-insensitively.</param>
    /// <returns><see langword="true"/> if the segment names an excluded
    /// directory; otherwise <see langword="false"/>.</returns>
    public static bool IsExcludedDirectoryName(ReadOnlySpan<char> name)
        => ExcludedDirectoriesLookup.Contains(name);

    /// <summary>
    /// Lazily streams the absolute path of every file under
    /// <paramref name="root"/>, descending the tree once and pruning
    /// excluded directories. Yields nothing when <paramref name="root"/>
    /// does not exist.
    /// </summary>
    /// <param name="root">Absolute path of the workspace root to
    /// walk.</param>
    /// <param name="cancellationToken">Cancels the walk; observed once
    /// per yielded file.</param>
    /// <returns>A lazy sequence of absolute file paths.</returns>
    public static IEnumerable<string> Walk(
        string root,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(root))
        {
            yield break;
        }

        using var enumerator = new WorkspaceFileEnumerator(root);

        while (enumerator.MoveNext())
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return enumerator.Current;
        }
    }

    /// <inheritdoc />
    protected override bool ContinueOnError(int error)
        => true;

    /// <inheritdoc />
    protected override bool ShouldIncludeEntry(ref FileSystemEntry entry)
        => !entry.IsDirectory;

    /// <inheritdoc />
    protected override bool ShouldRecurseIntoEntry(ref FileSystemEntry entry)
        => !IsExcludedDirectoryName(entry.FileName);

    /// <inheritdoc />
    protected override string TransformEntry(ref FileSystemEntry entry)
        => entry.ToFullPath();
}
