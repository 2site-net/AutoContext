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
}
