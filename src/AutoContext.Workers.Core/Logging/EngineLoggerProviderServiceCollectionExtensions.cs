namespace AutoContext.Workers.Core.Logging;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Registers the worker→engine logging pipeline —
/// <see cref="EngineWriteLogClient"/>, <see cref="EngineLogIngestRing"/>,
/// and the <see cref="EngineLoggerProvider"/> that feeds them — on a
/// service collection.
/// </summary>
public static class EngineLoggerProviderServiceCollectionExtensions
{
    /// <summary>
    /// Adds the worker→engine <see cref="ILoggerProvider"/> and its
    /// supporting client and ring as singletons. Records emitted
    /// through the resulting provider are stamped with
    /// <paramref name="workerId"/>'s routing prefix and shipped to
    /// the engine's <c>rpc</c> endpoint over <c>Engine.WriteLog</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="workerId">The worker's stable short identifier
    /// (for example <c>dotnet</c>).</param>
    /// <param name="engineRpcServiceAddress">Named-pipe address of the
    /// engine's <c>rpc</c> endpoint. Empty disables delivery (records
    /// buffer, then drop to stderr) without the caller special-casing
    /// standalone runs.</param>
    /// <returns><paramref name="services"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or
    /// <paramref name="engineRpcServiceAddress"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="workerId"/> is empty or whitespace.</exception>
    public static IServiceCollection AddEngineLoggerProvider(
        this IServiceCollection services,
        string workerId,
        string engineRpcServiceAddress)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);
        ArgumentNullException.ThrowIfNull(engineRpcServiceAddress);

        // The ring owns the client so the two tear down in the right
        // order (drain stops, then the connection closes). The client
        // uses a null logger on purpose: it sits beneath the logging
        // pipeline this very provider installs, so routing its own
        // diagnostics through ILogger would feed back into the ring it
        // drains.
        services.AddSingleton(_ => new EngineLogIngestRing(
            new EngineWriteLogClient(engineRpcServiceAddress, NullLogger<EngineWriteLogClient>.Instance),
            workerId,
            TimeProvider.System));
        services.AddSingleton<ILoggerProvider>(sp => new EngineLoggerProvider(
            workerId, sp.GetRequiredService<EngineLogIngestRing>(), TimeProvider.System));

        return services;
    }
}
