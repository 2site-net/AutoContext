namespace AutoContext.Instructions.Manifest.Generator;

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

/// <summary>
/// Serialises an <see cref="InstructionsManifest"/> to deterministic,
/// two-space-indented JSON with a trailing newline. Layout is pinned by
/// <see cref="InstructionsManifestJsonContext"/> and the shared relaxed encoder
/// from <see cref="InstructionsManifestJsonOptions"/>, so descriptions keep their
/// literal punctuation while the bytes stay stable across builds.
/// </summary>
internal sealed class InstructionsManifestSerializer : IInstructionsManifestSerializer
{
    private static readonly JsonTypeInfo<InstructionsManifest> ManifestTypeInfo =
        (JsonTypeInfo<InstructionsManifest>)InstructionsManifestJsonOptions.Relaxed.GetTypeInfo(typeof(InstructionsManifest));

    /// <inheritdoc />
    public string Serialize(InstructionsManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        return JsonSerializer.Serialize(manifest, ManifestTypeInfo) + "\n";
    }
}
