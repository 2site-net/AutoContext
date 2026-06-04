namespace AutoContext.Instructions.Manifest.Generator;

/// <summary>
/// Serialises an <see cref="InstructionsManifest"/> to deterministic,
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
    string Serialize(InstructionsManifest manifest);
}
