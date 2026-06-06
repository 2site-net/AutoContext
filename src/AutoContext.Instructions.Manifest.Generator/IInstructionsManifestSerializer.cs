namespace AutoContext.Instructions.Manifest.Generator;

/// <summary>
/// Serialises a <see cref="JsonInstructionsManifest"/> to deterministic,
/// byte-stable JSON text.
/// </summary>
internal interface IInstructionsManifestSerializer
{
    /// <summary>
    /// Serialises <paramref name="manifest"/> to JSON text.
    /// </summary>
    /// <param name="manifest">The manifest to serialise.</param>
    /// <returns>The JSON document, newline-terminated.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="manifest"/> is
    /// <see langword="null"/>.</exception>
    string Serialize(JsonInstructionsManifest manifest);
}
