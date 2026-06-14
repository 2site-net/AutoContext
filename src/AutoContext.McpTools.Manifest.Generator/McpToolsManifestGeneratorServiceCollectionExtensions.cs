namespace AutoContext.McpTools.Manifest.Generator;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the mcp-tools manifest generator and its collaborators on a host's
/// service collection.
/// </summary>
internal static class McpToolsManifestGeneratorServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="McpToolsManifestGenerator"/>,
    /// <see cref="IMcpToolsRegistryProjector"/>, and
    /// <see cref="IMcpToolsManifestSerializer"/> to <paramref name="services"/>.
    /// </summary>
    /// <param name="services">The service collection to extend.</param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is
    /// <see langword="null"/>.</exception>
    public static IServiceCollection AddMcpToolsManifestGenerator(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IMcpToolsRegistryProjector, McpToolsRegistryProjector>();
        services.AddSingleton<IMcpToolsManifestSerializer, McpToolsManifestSerializer>();
        services.AddSingleton<McpToolsManifestGenerator>();

        return services;
    }
}
