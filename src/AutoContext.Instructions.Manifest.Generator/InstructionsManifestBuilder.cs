namespace AutoContext.Instructions.Manifest.Generator;

using System.Text.RegularExpressions;

using AutoContext.Instructions.Parser;

/// <summary>
/// Builds the build-generated <c>instructions-manifest.json</c> fact index from an
/// already-parsed corpus. In a single pass it validates curatorial frontmatter
/// shape and extracts the derived facts (section map, <c>applyTo</c> extension set)
/// the engine merges with the hand-authored catalog at startup. Every file's
/// frontmatter, content hash, and changelog flag are read from the shared
/// <see cref="CorpusFileParsedResult"/> the <see cref="CorpusParser"/> already
/// produced, so the builder touches no disk and re-parses nothing. It validates
/// curatorial shape but never inspects glob semantics — <c>applyTo</c> is carried
/// verbatim onto the manifest, and only its concrete extensions are projected.
/// </summary>
internal sealed partial class InstructionsManifestBuilder : IInstructionsManifestBuilder
{
    private const string InstructionsFileSuffix = ".instructions.md";
    private const string SchemaVersion = "1";

    /// <inheritdoc />
    public JsonInstructionsManifest Build(IReadOnlyDictionary<string, CorpusFileParsedResult> corpus)
    {
        ArgumentNullException.ThrowIfNull(corpus);

        var entries = new List<JsonInstructionsManifestEntry>(corpus.Count);

        foreach (var file in corpus.Values)
        {
            entries.Add(BuildEntry(file));
        }

        entries.Sort(static (left, right) => string.CompareOrdinal(left.Key, right.Key));

        return new JsonInstructionsManifest(SchemaVersion, entries);
    }

    private static JsonInstructionsManifestEntry BuildEntry(CorpusFileParsedResult file)
    {
        var fileName = file.FileName;
        var parsed = file.Content;
        var frontmatter = parsed.Frontmatter;
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

        var extensions = ExtractExtensions(frontmatter.ApplyTo);
        var sections = ExtractSections(fileName, parsed.Body.Sections);

        return new JsonInstructionsManifestEntry(
            key,
            fileName,
            name,
            version,
            description,
            applyTo,
            extensions,
            file.HasChangelog,
            file.ContentHash,
            sections);
    }

    private static IReadOnlyList<string>? ExtractExtensions(FrontmatterApplyToParsedResult? applyTo)
    {
        if (applyTo is null)
        {
            return null;
        }

        return [.. applyTo.Extensions.OrderBy(static extension => extension, StringComparer.Ordinal)];
    }

    private static List<JsonInstructionsManifestSection> ExtractSections(
        string fileName,
        IReadOnlyList<InstructionsFileSection> parsedSections)
    {
        var sections = new List<JsonInstructionsManifestSection>(parsedSections.Count);
        var seenAnchors = new HashSet<string>(StringComparer.Ordinal);

        foreach (var section in parsedSections)
        {
            if (!seenAnchors.Add(section.Anchor))
            {
                throw Fail(fileName, "duplicate section anchor '" + section.Anchor + "' (heading collision)");
            }

            sections.Add(new JsonInstructionsManifestSection(section.Heading, section.Anchor, section.Parent));
        }

        return sections;
    }

    private static InvalidOperationException Fail(string fileName, string message)
        => new("[" + fileName + "] " + message);

    [GeneratedRegex(@"^([a-z0-9][a-z0-9-]*) \(v(\d+\.\d+\.\d+)\)$")]
    private static partial Regex GeneratedNamePatternRegex();
}
