namespace AutoContext.Instructions.Manifest.Generator;

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

/// <summary>
/// Serialises an <see cref="InstructionsMetadata"/> catalogue to deterministic,
/// two-space-indented JSON with a trailing newline. Layout is pinned by
/// <see cref="InstructionsManifestJsonContext"/> and the shared relaxed encoder
/// from <see cref="InstructionsManifestJsonOptions"/>, so descriptions keep their
/// literal punctuation while the bytes stay stable across builds.
/// </summary>
internal sealed class InstructionsMetadataSerializer : IInstructionsMetadataSerializer
{
    private static readonly JsonTypeInfo<InstructionsMetadata> MetadataTypeInfo =
        (JsonTypeInfo<InstructionsMetadata>)InstructionsManifestJsonOptions.Relaxed.GetTypeInfo(typeof(InstructionsMetadata));

    /// <inheritdoc />
    public string Serialize(InstructionsMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return JsonSerializer.Serialize(metadata, MetadataTypeInfo) + "\n";
    }
}
