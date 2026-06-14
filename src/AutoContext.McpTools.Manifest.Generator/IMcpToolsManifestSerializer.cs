namespace AutoContext.McpTools.Manifest.Generator;

/// <summary>
/// Serialises a <see cref="JsonMcpToolsCatalog"/> to deterministic JSON.
/// </summary>
internal interface IMcpToolsManifestSerializer
{
    /// <summary>Serialises <paramref name="catalog"/> to two-space-indented JSON with a trailing newline.</summary>
    /// <param name="catalog">The catalog to serialise.</param>
    /// <returns>The serialised JSON text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="catalog"/> is <see langword="null"/>.</exception>
    string Serialize(JsonMcpToolsCatalog catalog);
}
