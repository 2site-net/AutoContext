namespace AutoContext.Instructions.Manifest.Generator.Tests.Support;

using System.Text.Json;

using AutoContext.Instructions.Manifest.Generator;

/// <summary>
/// Writes an <c>instructions-catalog.json</c> into a temp directory so the
/// <see cref="InstructionsCatalogReader"/> can read it back, mirroring the way
/// <see cref="InstructionsCorpusTestWriter"/> seeds a corpus on disk.
/// </summary>
internal static class InstructionsCatalogTestWriter
{
    private const string CatalogFileName = "instructions-catalog.json";

    internal static string Write(string directory, JsonInstructionsCatalog catalog)
    {
        var json = JsonSerializer.Serialize(
            catalog,
            InstructionsManifestJsonContext.Default.JsonInstructionsCatalog);

        return WriteRaw(directory, json);
    }

    internal static string Write(
        string directory,
        IReadOnlyList<JsonInstructionsCatalogCategory> categories,
        params JsonInstructionsCatalogEntry[] entries)
        => Write(directory, InstructionsManifestFakeData.CreateCatalog(categories, entries));

    internal static string Write(
        string directory,
        IReadOnlyList<string> alwaysAttached,
        IReadOnlyList<JsonInstructionsCatalogCategory> categories,
        params JsonInstructionsCatalogEntry[] entries)
        => Write(directory, InstructionsManifestFakeData.CreateCatalog(alwaysAttached, categories, entries));

    internal static string WriteRaw(string directory, string json)
    {
        var path = Path.Combine(directory, CatalogFileName);
        File.WriteAllText(path, json);
        return path;
    }
}
