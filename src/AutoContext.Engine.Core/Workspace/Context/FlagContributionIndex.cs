namespace AutoContext.Engine.Core.Workspace.Context;

/// <summary>
/// A counted inverted index that maps each workspace file to the base
/// flags it raises and each base flag to its live contributor count.
/// Applying a path's new flag set adjusts both sides in lock-step, so the
/// active flag set always reflects exactly the files currently
/// contributing: the last contributor for a flag dropping out flips the
/// flag off, while a surviving sibling keeps it on. This lets
/// watcher-driven updates reclassify a single path in place instead of
/// re-scanning the workspace. The two dictionaries share a hard
/// invariant — every base flag's count equals the number of paths raising
/// it — which this type owns so callers cannot break it.
/// </summary>
internal sealed class FlagContributionIndex
{
    private readonly Dictionary<string, int> _baseCounts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlySet<string>> _contributions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The base flags that currently have at least one live contributor.
    /// Reflects the latest <see cref="Apply"/> calls; reading it does not
    /// run the activation cascade.
    /// </summary>
    public IReadOnlyCollection<string> ActiveFlags
        => _baseCounts.Keys;

    /// <summary>
    /// Replaces the flags attributed to <paramref name="path"/> with
    /// <paramref name="flags"/>, incrementing each newly raised flag and
    /// decrementing each flag the path no longer raises. Passing an empty
    /// set retracts the path entirely (e.g. when a file is deleted).
    /// </summary>
    /// <param name="path">Absolute path of the file whose contribution is
    /// being set.</param>
    /// <param name="flags">The flags <paramref name="path"/> now raises.
    /// Empty retracts any prior contribution.</param>
    public void Apply(string path, IReadOnlySet<string> flags)
    {
        _contributions.TryGetValue(path, out var oldFlags);

        if (oldFlags is not null)
        {
            foreach (var flag in oldFlags)
            {
                if (!flags.Contains(flag))
                {
                    Decrement(flag);
                }
            }
        }

        foreach (var flag in flags)
        {
            if (oldFlags is null || !oldFlags.Contains(flag))
            {
                _baseCounts[flag] = _baseCounts.GetValueOrDefault(flag) + 1;
            }
        }

        if (flags.Count == 0)
        {
            _contributions.Remove(path);
        }
        else
        {
            _contributions[path] = flags;
        }
    }

    /// <summary>
    /// Drops every recorded contribution, returning the index to its empty
    /// state ahead of a full re-scan.
    /// </summary>
    public void Clear()
    {
        _contributions.Clear();
        _baseCounts.Clear();
    }

    private void Decrement(string flag)
    {
        if (!_baseCounts.TryGetValue(flag, out var count))
        {
            return;
        }

        if (count <= 1)
        {
            _baseCounts.Remove(flag);
        }
        else
        {
            _baseCounts[flag] = count - 1;
        }
    }
}
