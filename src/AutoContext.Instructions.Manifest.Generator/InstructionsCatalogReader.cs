namespace AutoContext.Instructions.Manifest.Generator;

using System.Text;
using System.Text.Json;

/// <summary>
/// Reads the hand-authored <c>instructions-catalog.json</c> and cross-validates it
/// against the parsed corpus. The catalog is the curatorial representation (P3):
/// the generator never writes it, it only proves the hand-authored taxonomy and
/// the machine corpus still agree. Three reconciliations are build-fatal — an
/// entry naming a file that is not in the corpus (an orphan), a corpus file that
/// no entry catalogs (a stray, exempting the always-attached files the catalog
/// declares and the engine surfaces unconditionally), and an entry whose category
/// membership names a category the catalog never declares. The catalog's
/// <c>alwaysAttached</c> array is the single source of truth for that exempted set
/// (replacing the former hard-coded list); each entry must name a real corpus file
/// and must not also be cataloged. Authoring slips (a malformed document, a blank
/// label or file name, an entry with no categories, or a duplicated category, file
/// name, or always-attached entry) are fatal too, all reported with the
/// <c>[instructions-catalog.json] …</c> locator the orchestrator maps to exit 1.
/// </summary>
internal sealed class InstructionsCatalogReader : IInstructionsCatalogReader
{
    private const string CatalogLabel = "instructions-catalog.json";

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <inheritdoc />
    public JsonInstructionsCatalog Read(
        string catalogPath,
        IReadOnlyDictionary<string, CorpusFileParsedResult> corpus)
    {
        ArgumentNullException.ThrowIfNull(catalogPath);
        ArgumentNullException.ThrowIfNull(corpus);

        var catalog = Deserialize(catalogPath);

        var categories = ValidateCategories(catalog.Categories);
        var catalogedFiles = ValidateEntries(catalog.Instructions, categories);
        ValidateAlwaysAttached(catalog.AlwaysAttached, catalogedFiles);
        ReconcileWithCorpus(catalog.Instructions, catalog.AlwaysAttached, corpus);

        return catalog;
    }

    private static JsonInstructionsCatalog Deserialize(string catalogPath)
    {
        if (!File.Exists(catalogPath))
        {
            throw Fail("catalog file not found at '" + catalogPath + "'");
        }

        var text = File.ReadAllText(catalogPath, Utf8NoBom);

        JsonInstructionsCatalog? catalog;

        try
        {
            catalog = JsonSerializer.Deserialize(
                text,
                InstructionsManifestJsonContext.Default.JsonInstructionsCatalog);
        }
        catch (JsonException exception)
        {
            throw Fail("catalog is not valid JSON: " + exception.Message);
        }

        if (catalog is null)
        {
            throw Fail("catalog is empty");
        }

        if (catalog.Categories is null)
        {
            throw Fail("catalog is missing its `categories` array");
        }

        if (catalog.Instructions is null)
        {
            throw Fail("catalog is missing its `instructions` array");
        }

        if (catalog.AlwaysAttached is null)
        {
            throw Fail("catalog is missing its `alwaysAttached` array");
        }

        return catalog;
    }

    private static InvalidOperationException Fail(string message)
        => new("[" + CatalogLabel + "] " + message);

    private static void ReconcileWithCorpus(
        IReadOnlyList<JsonInstructionsCatalogEntry> entries,
        IReadOnlyList<string> alwaysAttached,
        IReadOnlyDictionary<string, CorpusFileParsedResult> corpus)
    {
        var corpusFiles = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in corpus.Values)
        {
            corpusFiles.Add(file.FileName);
        }

        var catalogedFiles = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            catalogedFiles.Add(entry.FileName);

            if (!corpusFiles.Contains(entry.FileName))
            {
                throw Fail("entry '" + entry.FileName + "' names a file that is not in the corpus");
            }
        }

        var alwaysAttachedFiles = new HashSet<string>(StringComparer.Ordinal);

        foreach (var fileName in alwaysAttached)
        {
            alwaysAttachedFiles.Add(fileName);

            if (!corpusFiles.Contains(fileName))
            {
                throw Fail("always-attached entry '" + fileName + "' names a file that is not in the corpus");
            }
        }

        foreach (var file in corpus.Values)
        {
            if (alwaysAttachedFiles.Contains(file.FileName))
            {
                continue;
            }

            if (!catalogedFiles.Contains(file.FileName))
            {
                throw Fail("corpus file '" + file.FileName + "' is not cataloged");
            }
        }
    }

    private static void ValidateAlwaysAttached(
        IReadOnlyList<string> alwaysAttached,
        HashSet<string> catalogedFiles)
    {
        var declared = new HashSet<string>(StringComparer.Ordinal);

        foreach (var fileName in alwaysAttached)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw Fail("an always-attached entry has a missing or blank file name");
            }

            if (!declared.Add(fileName))
            {
                throw Fail("duplicate always-attached entry '" + fileName + "'");
            }

            if (catalogedFiles.Contains(fileName))
            {
                throw Fail("file '" + fileName + "' is declared always-attached and also cataloged");
            }
        }
    }

    private static HashSet<string> ValidateCategories(IReadOnlyList<JsonInstructionsCatalogCategory> categories)
    {
        var declared = new HashSet<string>(StringComparer.Ordinal);

        foreach (var category in categories)
        {
            if (string.IsNullOrWhiteSpace(category.Name))
            {
                throw Fail("a category has a missing or blank `name`");
            }

            if (!declared.Add(category.Name))
            {
                throw Fail("duplicate category '" + category.Name + "'");
            }
        }

        return declared;
    }

    private static HashSet<string> ValidateEntries(
        IReadOnlyList<JsonInstructionsCatalogEntry> entries,
        HashSet<string> declaredCategories)
    {
        var seenFiles = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.FileName))
            {
                throw Fail("an entry has a missing or blank `fileName`");
            }

            if (string.IsNullOrWhiteSpace(entry.Label))
            {
                throw Fail("entry '" + entry.FileName + "' has a missing or blank `label`");
            }

            if (!seenFiles.Add(entry.FileName))
            {
                throw Fail("duplicate entry for file '" + entry.FileName + "'");
            }

            if (entry.Categories is null || entry.Categories.Count == 0)
            {
                throw Fail("entry '" + entry.FileName + "' declares no categories");
            }

            foreach (var category in entry.Categories)
            {
                if (!declaredCategories.Contains(category))
                {
                    throw Fail("entry '" + entry.FileName + "' references undeclared category '" + category + "'");
                }
            }
        }

        return seenFiles;
    }
}
