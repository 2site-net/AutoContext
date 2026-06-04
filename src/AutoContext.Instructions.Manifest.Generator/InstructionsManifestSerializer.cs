namespace AutoContext.Instructions.Manifest.Generator;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

/// <summary>
/// Serialises an <see cref="InstructionsManifest"/> to deterministic,
/// two-space-indented JSON with a trailing newline. Layout is pinned by
/// <see cref="InstructionsManifestJsonContext"/>; this serializer only layers on
/// the relaxed encoder (which cannot be an attribute constant), so descriptions
/// keep their literal punctuation while the bytes stay stable across builds.
/// </summary>
internal sealed class InstructionsManifestSerializer : IInstructionsManifestSerializer
{
    private static readonly JsonTypeInfo<InstructionsManifest> ManifestTypeInfo = CreateManifestTypeInfo();

    /// <inheritdoc />
    public string Serialize(InstructionsManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        return JsonSerializer.Serialize(manifest, ManifestTypeInfo) + "\n";
    }

    private static JsonTypeInfo<InstructionsManifest> CreateManifestTypeInfo()
    {
        var options = new JsonSerializerOptions(InstructionsManifestJsonContext.Default.Options)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        return (JsonTypeInfo<InstructionsManifest>)options.GetTypeInfo(typeof(InstructionsManifest));
    }
}
