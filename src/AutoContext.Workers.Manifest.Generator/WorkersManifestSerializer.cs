namespace AutoContext.Workers.Manifest.Generator;

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

/// <summary>
/// Serialises a <see cref="JsonWorkersManifest"/> to deterministic,
/// two-space-indented JSON with a trailing newline. Layout is pinned by
/// <see cref="WorkersManifestJsonContext"/> and the shared relaxed encoder from
/// <see cref="WorkersManifestJsonOptions"/>, so the bytes stay stable across
/// builds.
/// </summary>
internal sealed class WorkersManifestSerializer : IWorkersManifestSerializer
{
    private static readonly JsonTypeInfo<JsonWorkersManifest> ManifestTypeInfo =
        (JsonTypeInfo<JsonWorkersManifest>)WorkersManifestJsonOptions.Relaxed.GetTypeInfo(typeof(JsonWorkersManifest));

    /// <inheritdoc />
    public string Serialize(JsonWorkersManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        return JsonSerializer.Serialize(manifest, ManifestTypeInfo) + "\n";
    }
}
