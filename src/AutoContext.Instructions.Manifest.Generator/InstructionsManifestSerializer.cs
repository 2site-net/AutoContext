namespace AutoContext.Instructions.Manifest.Generator;

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

/// <summary>
/// Serialises a <see cref="JsonInstructionsManifest"/> to deterministic,
/// two-space-indented JSON with a trailing newline. Layout is pinned by
/// <see cref="InstructionsManifestJsonContext"/> and the shared relaxed encoder
/// from <see cref="InstructionsManifestJsonOptions"/>, so descriptions keep their
/// literal punctuation while the bytes stay stable across builds.
/// </summary>
internal sealed class InstructionsManifestSerializer : IInstructionsManifestSerializer
{
    private static readonly JsonTypeInfo<JsonInstructionsManifest> ManifestTypeInfo =
        (JsonTypeInfo<JsonInstructionsManifest>)InstructionsManifestJsonOptions.Relaxed.GetTypeInfo(typeof(JsonInstructionsManifest));

    /// <inheritdoc />
    public string Serialize(JsonInstructionsManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        return JsonSerializer.Serialize(manifest, ManifestTypeInfo) + "\n";
    }
}
