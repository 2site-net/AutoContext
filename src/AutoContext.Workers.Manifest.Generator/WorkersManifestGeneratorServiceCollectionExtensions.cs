namespace AutoContext.Workers.Manifest.Generator;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the workers-manifest generator and its collaborators on a host's
/// service collection.
/// </summary>
internal static class WorkersManifestGeneratorServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="WorkersManifestGenerator"/>,
    /// <see cref="IWorkerDescriptorScanner"/>, and
    /// <see cref="IWorkersManifestSerializer"/> to <paramref name="services"/>.
    /// </summary>
    /// <param name="services">The service collection to extend.</param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is
    /// <see langword="null"/>.</exception>
    public static IServiceCollection AddWorkersManifestGenerator(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IWorkerDescriptorScanner, WorkerDescriptorScanner>();
        services.AddSingleton<IWorkersManifestSerializer, WorkersManifestSerializer>();
        services.AddSingleton<WorkersManifestGenerator>();

        return services;
    }
}
