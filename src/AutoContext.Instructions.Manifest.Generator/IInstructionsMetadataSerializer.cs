namespace AutoContext.Instructions.Manifest.Generator;

/// <summary>
/// Serialises an <see cref="InstructionsMetadata"/> catalogue to deterministic,
/// byte-stable JSON text.
/// </summary>
internal interface IInstructionsMetadataSerializer
{
    /// <summary>
    /// Serialises <paramref name="metadata"/> to JSON text.
    /// </summary>
    /// <param name="metadata">The metadata catalogue to serialise.</param>
    /// <returns>The JSON document, newline-terminated.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="metadata"/> is
    /// <see langword="null"/>.</exception>
    string Serialize(InstructionsMetadata metadata);
}
