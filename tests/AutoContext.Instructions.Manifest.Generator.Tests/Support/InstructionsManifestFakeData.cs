namespace AutoContext.Instructions.Manifest.Generator.Tests.Support;

using AutoContext.Instructions.Manifest.Generator;

internal static class InstructionsManifestFakeData
{
    internal static JsonInstructionsManifestEntry CreateEntry(
        string key = "code-review",
        string fileName = "code-review.instructions.md",
        string name = "code-review (v1.0.0)",
        string version = "1.0.0",
        string description = "Apply when reviewing code.",
        string? applyTo = null,
        IReadOnlyList<string>? extensions = null,
        bool hasChangelog = false,
        string contentHash = "sha256:abc",
        IReadOnlyList<JsonInstructionsManifestSection>? sections = null)
        => new(key, fileName, name, version, description, applyTo, extensions, hasChangelog, contentHash, sections ?? []);

    internal static JsonInstructionsManifest CreateManifest(params JsonInstructionsManifestEntry[] entries)
        => new("1", entries);

    internal static JsonInstructionsManifestSection CreateSection(
        string heading = "Heading",
        string anchor = "heading",
        string? parent = null)
        => new(heading, anchor, parent);

    internal static JsonInstructionsCatalogCategory CreateCategory(
        string name = "General",
        string description = "General guidance.")
        => new(name, description);

    internal static JsonInstructionsCatalogEntry CreateCatalogEntry(
        string label = "Code Review",
        string fileName = "code-review.instructions.md",
        IReadOnlyList<string>? categories = null,
        IReadOnlyList<string>? activationFlags = null)
        => new(label, fileName, categories ?? ["General"], activationFlags);

    internal static JsonInstructionsCatalog CreateCatalog(
        IReadOnlyList<JsonInstructionsCatalogCategory> categories,
        params JsonInstructionsCatalogEntry[] entries)
        => new("1", [], categories, entries);

    internal static JsonInstructionsCatalog CreateCatalog(
        IReadOnlyList<string> alwaysAttached,
        IReadOnlyList<JsonInstructionsCatalogCategory> categories,
        params JsonInstructionsCatalogEntry[] entries)
        => new("1", alwaysAttached, categories, entries);
}
