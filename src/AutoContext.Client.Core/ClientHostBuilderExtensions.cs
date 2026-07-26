namespace AutoContext.Client.Core;

using AutoContext.Client.Core.Engine;
using AutoContext.Client.Core.Engine.Rpc;
using AutoContext.Framework.Pipes;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

/// <summary>
/// Composition root for <c>AutoContext.Client.Core</c>. Registering
/// the client with <see cref="AddAutoContextClient"/> binds
/// <see cref="ClientOptions"/> into the host's options pipeline,
/// installs its shape validator, and registers the find-or-spawn
/// resolver together with the transport and spawn seam it composes.
/// Mirror of the engine's <c>AddAutoContextEngine</c>: this is the
/// library's single public entry point, called by the
/// <c>autocontext</c> CLI binary, by tests, and by third-party .NET
/// embedders — and by nothing else.
/// </summary>
public static class ClientHostBuilderExtensions
{
    /// <summary>
    /// Registers the AutoContext engine client on
    /// <paramref name="builder"/>'s service collection.
    /// </summary>
    /// <param name="builder">Host application builder to extend. Must
    /// not be <see langword="null"/>.</param>
    /// <param name="configure">Callback that mutates the
    /// <see cref="ClientOptions"/> instance before it is validated.
    /// Must not be <see langword="null"/>.</param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/> or <paramref name="configure"/> is
    /// <see langword="null"/>.</exception>
    public static IHostApplicationBuilder AddAutoContextClient(
        this IHostApplicationBuilder builder,
        Action<ClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddOptions<ClientOptions>()
            .Configure(configure)
            .ValidateOnStart();

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<ClientOptions>, ClientOptionsValidator>());

        builder.Services.TryAddSingleton<PipeTransport>();
        builder.Services.TryAddSingleton(EngineConnectBudget.Default);
        builder.Services.TryAddSingleton<IEngineSpawner, EngineSpawner>();
        builder.Services.TryAddSingleton<EngineConnector>();

        builder.Services.TryAddSingleton<Func<CancellationToken, Task<EngineClient>>>(
            serviceProvider => cancellationToken =>
                EngineClient.ConnectAsync(
                    serviceProvider.GetRequiredService<EngineConnector>(), cancellationToken));

        return builder;
    }
}
