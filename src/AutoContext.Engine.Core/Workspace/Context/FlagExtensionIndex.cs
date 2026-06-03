namespace AutoContext.Engine.Core.Workspace.Context;

using System.Collections.Frozen;

/// <summary>
/// Maps each file-rule flag to the file extensions its
/// <see cref="FileSelectorKind.Extension"/> selectors name, and resolves
/// an active flag set into the distinct extension index the coarse
/// <c>applyTo</c> filter consumes. Built once from the
/// <see cref="FilePresenceRule"/> table, so adding a new file-rule flag
/// automatically widens the derived extension set. File-name and glob
/// selectors are ignored, and content-scan flags contribute nothing,
/// since only file extensions feed the filter.
/// </summary>
internal sealed class FlagExtensionIndex
{
    private readonly FrozenDictionary<string, string[]> _flagToExtensions;

    /// <summary>
    /// Builds the flag-to-extensions index from the supplied file-presence
    /// rules, keeping only each rule's
    /// <see cref="FileSelectorKind.Extension"/> selectors.
    /// </summary>
    /// <param name="fileRules">File-presence rules whose extension
    /// selectors are indexed by flag. Each flag must appear at most once;
    /// the rule model bundles all of a flag's selectors into a single
    /// rule.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fileRules"/>
    /// is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Two extension-bearing rules name
    /// the same flag, which would silently drop one row's extensions.</exception>
    public FlagExtensionIndex(IReadOnlyList<FilePresenceRule> fileRules)
    {
        ArgumentNullException.ThrowIfNull(fileRules);

        var map = new Dictionary<string, string[]>(StringComparer.Ordinal);

        foreach (var rule in fileRules)
        {
            var extensions = rule.Selectors
                .Where(static selector => selector.Kind == FileSelectorKind.Extension)
                .Select(static selector => selector.Value)
                .ToArray();

            if (extensions.Length == 0)
            {
                continue;
            }

            if (!map.TryAdd(rule.Flag, extensions))
            {
                throw new ArgumentException(
                    $"Duplicate file-rule flag '{rule.Flag}'.", nameof(fileRules));
            }
        }

        _flagToExtensions = map.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>
    /// Resolves <paramref name="activeFlags"/> into the sorted, distinct
    /// union of the file extensions named by every active file-rule flag.
    /// Flags with no extension selectors — including content-scan and
    /// activation-cascade flags — contribute nothing.
    /// </summary>
    /// <param name="activeFlags">The flag set raised by a detection
    /// pass.</param>
    /// <returns>The de-duplicated extensions in ordinal order; empty when
    /// no active flag names an extension.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="activeFlags"/>
    /// is <see langword="null"/>.</exception>
    public IReadOnlyList<string> Resolve(IReadOnlySet<string> activeFlags)
    {
        ArgumentNullException.ThrowIfNull(activeFlags);

        var extensions = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var flag in activeFlags)
        {
            if (_flagToExtensions.TryGetValue(flag, out var flagExtensions))
            {
                foreach (var extension in flagExtensions)
                {
                    extensions.Add(extension);
                }
            }
        }

        return [.. extensions];
    }
}
