namespace AutoContext.Instructions.Manifest.Generator;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the instructions-manifest generator and its collaborators on a
/// host's service collection.
/// </summary>
internal static class InstructionsManifestGeneratorServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="InstructionsManifestGenerator"/>,
    /// <see cref="ICorpusParser"/>,
    /// <see cref="IInstructionsListBuilder"/>,
    /// <see cref="IInstructionsManifestSerializer"/>,
    /// <see cref="IInstructionsMetadataBuilder"/>,
    /// <see cref="IInstructionsMetadataSerializer"/>, and
    /// <see cref="IInstructionsReferenceValidator"/> to
    /// <paramref name="services"/>.
    /// </summary>
    /// <param name="services">The service collection to extend.</param>
    /// <returns><paramref name="services"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is
    /// <see langword="null"/>.</exception>
    public static IServiceCollection AddInstructionsManifestGenerator(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ICorpusParser, CorpusParser>();
        services.AddSingleton<IInstructionsListBuilder, InstructionsListBuilder>();
        services.AddSingleton<IInstructionsManifestSerializer, InstructionsManifestSerializer>();
        services.AddSingleton<IInstructionsMetadataBuilder, InstructionsMetadataBuilder>();
        services.AddSingleton<IInstructionsMetadataSerializer, InstructionsMetadataSerializer>();
        services.AddSingleton<IInstructionsReferenceValidator, InstructionsReferenceValidator>();
        services.AddSingleton<InstructionsManifestGenerator>();

        return services;
    }
}
