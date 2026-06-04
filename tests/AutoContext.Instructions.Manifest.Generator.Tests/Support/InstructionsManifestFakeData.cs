namespace AutoContext.Instructions.Manifest.Generator.Tests.Support;

using AutoContext.Instructions.Manifest.Generator;

internal static class InstructionsManifestFakeData
{
    internal static InstructionsManifestEntry CreateEntry(
        string key = "code-review",
        string fileName = "code-review.instructions.md",
        string name = "code-review (v1.0.0)",
        string version = "1.0.0",
        string description = "Apply when reviewing code.",
        string? applyTo = null,
        bool hasChangelog = false,
        string contentHash = "sha256:abc",
        bool alwaysAttached = false)
        => new(key, fileName, name, version, description, applyTo, hasChangelog, contentHash, alwaysAttached);

    internal static InstructionsManifest CreateManifest(params InstructionsManifestEntry[] entries)
        => new("1", entries);

    internal static InstructionsMetadataSection CreateSection(
        string heading = "Heading",
        string anchor = "heading",
        string? parent = null)
        => new(heading, anchor, parent);

    internal static InstructionsMetadataEntry CreateMetadataEntry(
        string key = "code-review",
        string fileName = "code-review.instructions.md",
        string name = "code-review (v1.0.0)",
        string version = "1.0.0",
        string description = "Apply when reviewing code.",
        string? applyTo = null,
        IReadOnlyList<string>? extensions = null,
        bool hasChangelog = false,
        string contentHash = "sha256:abc",
        IReadOnlyList<InstructionsMetadataSection>? sections = null)
        => new(key, fileName, name, version, description, applyTo, extensions, hasChangelog, contentHash, sections ?? []);

    internal static InstructionsMetadata CreateMetadata(params InstructionsMetadataEntry[] entries)
        => new("1", entries);
}
