namespace AutoContext.Instructions.Manifest.Generator;

using System.Text.Encodings.Web;
using System.Text.Json;

/// <summary>
/// The single relaxed <see cref="JsonSerializerOptions"/> instance the manifest
/// serializers share. It layers the only non-attribute layout knob — the relaxed
/// encoder, which keeps literal punctuation in descriptions instead of escaping
/// it — onto the source-generated defaults pinned by
/// <see cref="InstructionsManifestJsonContext"/>. The wire and metadata
/// serializers both resolve their <c>JsonTypeInfo</c> from these options so the
/// two catalogues stay byte-for-byte consistent across builds.
/// </summary>
internal static class InstructionsManifestJsonOptions
{
    /// <summary>Gets the shared relaxed serializer options.</summary>
    internal static JsonSerializerOptions Relaxed { get; } = CreateRelaxed();

    private static JsonSerializerOptions CreateRelaxed()
        => new(InstructionsManifestJsonContext.Default.Options)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
}
