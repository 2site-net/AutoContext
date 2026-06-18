namespace AutoContext.Engine.Core.Tests.Support.Features.Instructions;

using AutoContext.Engine.Core.Features.Instructions.Snapshot;

/// <summary>
/// Builds <see cref="InstructionsFileManifestEntry"/> instances for tests that
/// only care about the identity fields the body projection reads
/// (<see cref="InstructionsFileManifestEntry.Key"/> and
/// <see cref="InstructionsFileManifestEntry.FileName"/>); every other required
/// field is filled with an inert placeholder.
/// </summary>
internal static class InstructionsFileManifestEntryTestFactory
{
    public static InstructionsFileManifestEntry Create(
        string key,
        string? fileName = null,
        string? description = null,
        bool alwaysAttached = false,
        string? applyTo = null,
        IReadOnlyList<string>? extensions = null,
        string? category = null,
        string? label = null,
        IReadOnlyList<InstructionsSection>? sections = null)
        => new()
        {
            Key = key,
            FileName = fileName ?? $"{key}.instructions.md",
            Name = $"{key} (v1.0.0)",
            Version = "1.0.0",
            Description = description ?? $"{key} description.",
            ApplyTo = applyTo,
            Extensions = extensions,
            HasChangelog = false,
            ContentHash = "sha256:0",
            AlwaysAttached = alwaysAttached,
            Label = label,
            Category = category,
            Sections = sections ?? [],
        };
}
