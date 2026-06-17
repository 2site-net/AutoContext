namespace AutoContext.Engine.Core.Features.Instructions;

using System.Text.Json;

using AutoContext.Engine.Core.Features.Instructions.Format;
using AutoContext.Engine.Core.Features.Instructions.Snapshot;
using AutoContext.Engine.Core.Infrastructure;

/// <summary>
/// Loads the bundled instructions corpus into an immutable
/// <see cref="InstructionsManifestSnapshot"/> by reading and merging the
/// two build-time side-cars shipped beside the engine binary:
/// <c>instructions-manifest.json</c> (the generated fact index, the join
/// spine) and <c>instructions-catalog.json</c> (the hand-authored
/// curatorial layer — labels, category taxonomy and membership, and the
/// always-attached list).
/// </summary>
/// <remarks>
/// The side-cars are engine build artifacts cross-validated by the
/// generator, not user input: a missing file, malformed JSON, a manifest
/// row missing a required field, or a non-always-attached file with no
/// catalog entry is a packaging defect, so the loader throws rather than
/// degrading to a partial corpus. Failing here fails engine startup
/// loudly, which is the intended behaviour. Always-attached membership is
/// derived from the catalog's <c>alwaysAttached</c> list; those files
/// carry no label or category membership.
/// </remarks>
internal static class InstructionsManifestLoader
{
    /// <summary>The generated fact-index side-car file name.</summary>
    public const string ManifestFileName = "instructions-manifest.json";

    /// <summary>The hand-authored curatorial side-car file name.</summary>
    public const string CatalogFileName = "instructions-catalog.json";

    /// <summary>
    /// Reads the two side-cars from <paramref name="resources"/> and merges
    /// them into a corpus snapshot.
    /// </summary>
    /// <param name="resources">The resources directory holding both
    /// side-cars (override copies shadow the bundled ones). Must not be
    /// <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the file reads.</param>
    /// <returns>The loaded, immutable corpus snapshot.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="resources"/> is <see langword="null"/>.</exception>
    /// <exception cref="FileNotFoundException">A side-car is
    /// missing.</exception>
    /// <exception cref="InvalidOperationException">A side-car is
    /// malformed, a manifest row is missing a required field, or a
    /// non-always-attached file has no catalog entry.</exception>
    public static async Task<InstructionsManifestSnapshot> LoadAsync(
        EngineResourcesDirectory resources,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resources);

        var manifestPath = resources.ResolveFile(ManifestFileName);
        var catalogPath = resources.ResolveFile(CatalogFileName);

        var manifest = await ReadAsync(
            manifestPath,
            InstructionsManifestJsonContext.Default.JsonInstructionsManifest,
            cancellationToken).ConfigureAwait(false);
        var catalog = await ReadAsync(
            catalogPath,
            InstructionsManifestJsonContext.Default.JsonInstructionsCatalog,
            cancellationToken).ConfigureAwait(false);

        var categories = BuildCategories(catalog, catalogPath);
        var alwaysAttached = BuildAlwaysAttachedSet(catalog);
        var catalogByFileName = IndexCatalogByFileName(catalog, catalogPath);

        var rows = manifest.Instructions
            ?? throw Malformed(manifestPath, "the 'instructions' array is missing.");

        var files = new List<InstructionsFileManifestEntry>(rows.Count);

        foreach (var row in rows)
        {
            files.Add(Merge(
                row, alwaysAttached, catalogByFileName, manifestPath, catalogPath));
        }

        return new InstructionsManifestSnapshot(categories, files);
    }

    private static async Task<T> ReadAsync<T>(
        string path,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
        where T : class
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Bundled instruction side-car not found: '{path}'.", path);
        }

        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);

        T? parsed;

        try
        {
            parsed = JsonSerializer.Deserialize(bytes, typeInfo);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Bundled instruction side-car '{path}' is not valid JSON.", exception);
        }

        return parsed ?? throw Malformed(path, "it deserialised to null.");
    }

    private static List<InstructionsCategory> BuildCategories(
        JsonInstructionsCatalog catalog,
        string catalogPath)
    {
        var rows = catalog.Categories
            ?? throw Malformed(catalogPath, "the 'categories' array is missing.");

        var categories = new List<InstructionsCategory>(rows.Count);

        foreach (var row in rows)
        {
            categories.Add(new InstructionsCategory
            {
                Name = Required(row.Name, "categories[].name", catalogPath),
                Description = Required(row.Description, "categories[].description", catalogPath),
            });
        }

        return categories;
    }

    private static HashSet<string> BuildAlwaysAttachedSet(JsonInstructionsCatalog catalog)
        => new(catalog.AlwaysAttached ?? [], StringComparer.Ordinal);

    private static Dictionary<string, JsonInstructionsCatalogEntry> IndexCatalogByFileName(
        JsonInstructionsCatalog catalog,
        string catalogPath)
    {
        var rows = catalog.Instructions
            ?? throw Malformed(catalogPath, "the 'instructions' array is missing.");

        var byFileName = new Dictionary<string, JsonInstructionsCatalogEntry>(
            rows.Count, StringComparer.Ordinal);

        foreach (var row in rows)
        {
            var fileName = Required(row.FileName, "instructions[].fileName", catalogPath);

            if (!byFileName.TryAdd(fileName, row))
            {
                throw Malformed(
                    catalogPath, $"the file name '{fileName}' appears more than once.");
            }
        }

        return byFileName;
    }

    private static InstructionsFileManifestEntry Merge(
        JsonInstructionsManifestEntry manifest,
        HashSet<string> alwaysAttached,
        IReadOnlyDictionary<string, JsonInstructionsCatalogEntry> catalogByFileName,
        string manifestPath,
        string catalogPath)
    {
        var key = Required(manifest.Key, "key", manifestPath);
        var fileName = Required(manifest.FileName, "fileName", manifestPath);
        var isAlwaysAttached = alwaysAttached.Contains(fileName);

        var (label, categories, activationFlags) = isAlwaysAttached
            ? (null, [], [])
            : ResolveCatalog(fileName, catalogByFileName, catalogPath);

        return new InstructionsFileManifestEntry
        {
            Key = key,
            FileName = fileName,
            Name = Required(manifest.Name, "name", manifestPath),
            Version = Required(manifest.Version, "version", manifestPath),
            Description = Required(manifest.Description, "description", manifestPath),
            ApplyTo = manifest.ApplyTo,
            Extensions = manifest.Extensions,
            HasChangelog = manifest.HasChangelog,
            ContentHash = Required(manifest.ContentHash, "contentHash", manifestPath),
            AlwaysAttached = isAlwaysAttached,
            Label = label,
            Categories = categories,
            ActivationFlags = activationFlags,
            Sections = ProjectSections(manifest.Sections, key, manifestPath),
        };
    }

    private static (string? Label, IReadOnlyList<string> Categories, IReadOnlyList<string> ActivationFlags)
        ResolveCatalog(
            string fileName,
            IReadOnlyDictionary<string, JsonInstructionsCatalogEntry> catalogByFileName,
            string catalogPath)
    {
        if (!catalogByFileName.TryGetValue(fileName, out var entry))
        {
            throw Malformed(
                catalogPath, $"the file '{fileName}' has no catalog entry.");
        }

        return (
            Required(entry.Label, $"instructions[].label for '{fileName}'", catalogPath),
            entry.Categories ?? [],
            entry.ActivationFlags ?? []);
    }

    private static List<InstructionsSection> ProjectSections(
        IReadOnlyList<JsonInstructionsManifestSection>? sections,
        string key,
        string manifestPath)
    {
        if (sections is null || sections.Count == 0)
        {
            return [];
        }

        var projected = new List<InstructionsSection>(sections.Count);

        foreach (var section in sections)
        {
            projected.Add(new InstructionsSection
            {
                Heading = Required(section.Heading, $"sections[].heading for '{key}'", manifestPath),
                Anchor = Required(section.Anchor, $"sections[].anchor for '{key}'", manifestPath),
                Parent = section.Parent,
            });
        }

        return projected;
    }

    private static string Required(string? value, string field, string path)
        => string.IsNullOrEmpty(value)
            ? throw Malformed(path, $"the required field '{field}' is missing or empty.")
            : value;

    private static InvalidOperationException Malformed(string path, string reason)
        => new($"Bundled instruction side-car '{path}' is malformed: {reason}");
}
