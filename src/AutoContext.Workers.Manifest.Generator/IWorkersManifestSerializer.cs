namespace AutoContext.Workers.Manifest.Generator;

/// <summary>
/// Serialises a <see cref="JsonWorkersManifest"/> to deterministic JSON.
/// </summary>
internal interface IWorkersManifestSerializer
{
    /// <summary>Serialises <paramref name="manifest"/> to two-space-indented JSON with a trailing newline.</summary>
    /// <param name="manifest">The manifest to serialise.</param>
    /// <returns>The serialised JSON text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="manifest"/> is <see langword="null"/>.</exception>
    string Serialize(JsonWorkersManifest manifest);
}
