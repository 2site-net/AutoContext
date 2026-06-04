namespace AutoContext.Instructions.Manifest.Generator;

using System.Text;

using AutoContext.Instructions.Parser;

/// <summary>
/// Builds the catalogue-only <c>instructions-files-metadata.json</c> index by
/// enriching an already-validated <see cref="InstructionsManifest"/> with each
/// file's <c>##</c>/<c>###</c> section map and the parsed <c>applyTo</c> extension
/// set. The shared <see cref="InstructionsFileParser"/> yields both, so build-time
/// catalogue generation and runtime parsing observe one parse semantics. The
/// shape-validating work (name, key, description, hash) already happened while
/// the wire manifest was built; this builder only adds the engine-internal
/// indices and guards against heading-slug collisions.
/// </summary>
internal sealed class InstructionsMetadataBuilder : IInstructionsMetadataBuilder
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <inheritdoc />
    public InstructionsMetadata Build(InstructionsManifest manifest, string corpusDirectory)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(corpusDirectory);

        var entries = new List<InstructionsMetadataEntry>(manifest.Instructions.Count);

        foreach (var entry in manifest.Instructions)
        {
            entries.Add(BuildEntry(corpusDirectory, entry));
        }

        return new InstructionsMetadata(manifest.SchemaVersion, entries);
    }

    private static InstructionsMetadataEntry BuildEntry(string corpusDirectory, InstructionsManifestEntry entry)
    {
        var content = File.ReadAllText(Path.Combine(corpusDirectory, entry.FileName), Utf8NoBom);
        var parsed = InstructionsFileParser.Parse(content);
        var sections = ExtractSections(entry.FileName, parsed.Body.Sections);
        var extensions = ExtractExtensions(parsed.Frontmatter.ApplyTo);

        return new InstructionsMetadataEntry(
            entry.Key,
            entry.FileName,
            entry.Name,
            entry.Version,
            entry.Description,
            entry.ApplyTo,
            extensions,
            entry.HasChangelog,
            entry.ContentHash,
            sections);
    }

    private static string[]? ExtractExtensions(FrontmatterApplyToParsedResult? applyTo)
    {
        if (applyTo is null)
        {
            return null;
        }

        return [.. applyTo.Extensions.OrderBy(static extension => extension, StringComparer.Ordinal)];
    }

    private static List<InstructionsMetadataSection> ExtractSections(
        string fileName,
        IReadOnlyList<InstructionsFileSection> parsedSections)
    {
        var sections = new List<InstructionsMetadataSection>(parsedSections.Count);
        var seenAnchors = new HashSet<string>(StringComparer.Ordinal);

        foreach (var section in parsedSections)
        {
            if (!seenAnchors.Add(section.Anchor))
            {
                throw Fail(fileName, "duplicate section anchor '" + section.Anchor + "' (heading collision)");
            }

            sections.Add(new InstructionsMetadataSection(section.Heading, section.Anchor, section.Parent));
        }

        return sections;
    }

    private static InvalidOperationException Fail(string fileName, string message)
        => new("[" + fileName + "] " + message);
}
