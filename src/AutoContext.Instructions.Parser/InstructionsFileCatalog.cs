namespace AutoContext.Instructions.Parser;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// The whole-corpus index a cross-file reference resolves against: every
/// instructions file's rule ids and section index, keyed by catalogue key. The
/// catalogue is a pure value built once from already-parsed files; assembling it
/// from disk (walking the corpus, reading and parsing each file) is a caller
/// concern, so the resolver stays free of I/O and trivially testable.
/// </summary>
public sealed class InstructionsFileCatalog
{
    private readonly Dictionary<string, InstructionsFileCatalogEntry> _entriesByKey;

    /// <summary>
    /// Creates a catalogue from its per-file entries.
    /// </summary>
    /// <param name="entries">One entry per instructions file. Keys must be
    /// unique.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Two entries share a
    /// <see cref="InstructionsFileCatalogEntry.Key"/>.</exception>
    public InstructionsFileCatalog(IEnumerable<InstructionsFileCatalogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var map = new Dictionary<string, InstructionsFileCatalogEntry>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            if (!map.TryAdd(entry.Key, entry))
            {
                throw new ArgumentException(
                    $"Duplicate catalogue key '{entry.Key}'.",
                    nameof(entries));
            }
        }

        _entriesByKey = map;
    }

    /// <summary>
    /// Projects a set of parsed instructions files into a catalogue: each file's
    /// tagged rule ids and section index become its entry.
    /// </summary>
    /// <param name="parsedByKey">The parsed files, keyed by catalogue key.</param>
    /// <returns>The assembled catalogue.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parsedByKey"/> is
    /// <see langword="null"/>.</exception>
    public static InstructionsFileCatalog FromParsed(
        IReadOnlyDictionary<string, InstructionsFileParsedResult> parsedByKey)
    {
        ArgumentNullException.ThrowIfNull(parsedByKey);

        var entries = new List<InstructionsFileCatalogEntry>(parsedByKey.Count);

        foreach (var (key, parsed) in parsedByKey)
        {
            var ruleIds = parsed.Body.Rules
                .Select(rule => rule.Id)
                .OfType<string>()
                .ToHashSet(StringComparer.Ordinal);

            entries.Add(new InstructionsFileCatalogEntry(key, ruleIds, parsed.Body.Sections));
        }

        return new InstructionsFileCatalog(entries);
    }

    /// <summary>
    /// Looks up the entry for a catalogue key.
    /// </summary>
    /// <param name="key">The catalogue key (e.g. <c>testing</c>).</param>
    /// <param name="entry">The matching entry when found; otherwise
    /// <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the key is present.</returns>
    public bool TryGet(string key, [NotNullWhen(true)] out InstructionsFileCatalogEntry? entry)
        => _entriesByKey.TryGetValue(key, out entry);
}
