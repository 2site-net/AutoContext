namespace AutoContext.Instructions.Manifest.Generator;

using System.Text.RegularExpressions;

/// <summary>
/// Builds the wire-shape <c>instructions-files.json</c> catalogue from an
/// already-parsed corpus. The builder is the build-side library named by the
/// engine design; <see cref="InstructionsManifestGenerator"/> drives it from the
/// host entry point. Every file's frontmatter, content hash, and changelog flag
/// are read from the shared <see cref="CorpusFileParsedResult"/> the
/// <see cref="CorpusParser"/> already produced, so the builder touches no disk and
/// re-parses nothing. It validates curatorial shape, but deliberately never
/// inspects glob semantics — <c>applyTo</c> is carried verbatim onto the wire.
/// </summary>
internal sealed partial class InstructionsListBuilder : IInstructionsListBuilder
{
    private const string InstructionsFileSuffix = ".instructions.md";
    private const string SchemaVersion = "1";

    private static readonly HashSet<string> AlwaysAttachedFiles =
        new(StringComparer.Ordinal)
        {
            "copilot.instructions.md",
            "autocontext.instructions.md",
        };

    /// <inheritdoc />
    public InstructionsManifest Build(IReadOnlyDictionary<string, CorpusFileParsedResult> corpus)
    {
        ArgumentNullException.ThrowIfNull(corpus);

        var entries = new List<InstructionsManifestEntry>(corpus.Count);

        foreach (var file in corpus.Values)
        {
            entries.Add(BuildEntry(file));
        }

        entries.Sort(static (left, right) => string.CompareOrdinal(left.Key, right.Key));

        return new InstructionsManifest(SchemaVersion, entries);
    }

    private static InstructionsManifestEntry BuildEntry(CorpusFileParsedResult file)
    {
        var fileName = file.FileName;
        var frontmatter = file.Content.Frontmatter;
        var name = frontmatter.Name;

        if (name is null || name.Length == 0)
        {
            throw Fail(fileName, "missing required `name` frontmatter field");
        }

        var nameMatch = GeneratedNamePatternRegex().Match(name);

        if (!nameMatch.Success)
        {
            throw Fail(fileName, "`name` does not match `<key> (vX.Y.Z)`: '" + name + "'");
        }

        var key = nameMatch.Groups[1].Value;
        var version = nameMatch.Groups[2].Value;

        var expectedKey = fileName[..^InstructionsFileSuffix.Length];

        if (!string.Equals(key, expectedKey, StringComparison.Ordinal))
        {
            throw Fail(fileName, "`name` key '" + key + "' does not equal file basename '" + expectedKey + "'");
        }

        var description = frontmatter.Description?.Trim();

        if (description is null || description.Length == 0)
        {
            throw Fail(fileName, "missing or empty `description` frontmatter field");
        }

        var applyTo = frontmatter.ApplyTo?.RawValue;

        if (applyTo is not null && applyTo.AsSpan().Trim().IsEmpty)
        {
            throw Fail(fileName, "`applyTo` is present but empty");
        }

        var contentHash = file.ContentHash;
        var hasChangelog = file.HasChangelog;
        var alwaysAttached = AlwaysAttachedFiles.Contains(fileName);

        return new InstructionsManifestEntry(
            key,
            fileName,
            name,
            version,
            description,
            applyTo,
            hasChangelog,
            contentHash,
            alwaysAttached);
    }

    private static InvalidOperationException Fail(string fileName, string message)
        => new("[" + fileName + "] " + message);

    [GeneratedRegex(@"^([a-z0-9][a-z0-9-]*) \(v(\d+\.\d+\.\d+)\)$")]
    private static partial Regex GeneratedNamePatternRegex();
}
