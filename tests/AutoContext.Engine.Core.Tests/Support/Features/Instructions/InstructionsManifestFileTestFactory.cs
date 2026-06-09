namespace AutoContext.Engine.Core.Tests.Support.Features.Instructions;

using AutoContext.Engine.Core.Features.Instructions.Snapshot;

/// <summary>
/// Builds <see cref="InstructionsManifestFile"/> instances for tests that
/// only care about the identity fields the body projection reads
/// (<see cref="InstructionsManifestFile.Key"/> and
/// <see cref="InstructionsManifestFile.FileName"/>); every other required
/// field is filled with an inert placeholder.
/// </summary>
internal static class InstructionsManifestFileTestFactory
{
    public static InstructionsManifestFile Create(
        string key,
        string? fileName = null,
        string? description = null)
        => new()
        {
            Key = key,
            FileName = fileName ?? $"{key}.instructions.md",
            Name = $"{key} (v1.0.0)",
            Version = "1.0.0",
            Description = description ?? $"{key} description.",
            HasChangelog = false,
            ContentHash = "sha256:0",
            AlwaysAttached = false,
        };
}
