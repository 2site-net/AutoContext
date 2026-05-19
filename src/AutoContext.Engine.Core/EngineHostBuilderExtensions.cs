namespace AutoContext.Engine.Core;

using AutoContext.Engine.Core.Lifecycle;
using AutoContext.Engine.Core.Registry;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

/// <summary>
/// Composition root for <c>AutoContext.Engine.Core</c>. Registering
/// the engine with <see cref="AddAutoContextEngine"/> binds
/// <see cref="EngineOptions"/> into the host's options pipeline,
/// installs the shape validator, and prepares the DI graph for the
/// engine's hosted services. The engine binary's <c>Program.Main</c>
/// and every test harness call this method; nothing else does.
/// </summary>
/// <remarks>
/// Per <c>design § Composition contracts</c> this method is the
/// engine library's <i>single</i> public entry point. Adding new
/// engine capabilities means adding new hosted services and DI
/// registrations behind this call, never new top-level extension
/// methods.
/// </remarks>
public static class EngineHostBuilderExtensions
{
    /// <summary>
    /// Registers the AutoContext engine on
    /// <paramref name="builder"/>'s service collection.
    /// </summary>
    /// <param name="builder">Host application builder to extend.
    /// Must not be <see langword="null"/>.</param>
    /// <param name="configure">Callback that mutates the
    /// <see cref="EngineOptions"/> instance before it is validated.
    /// The callback runs once when the options pipeline first
    /// materialises the instance. Must not be <see langword="null"/>.</param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/> or <paramref name="configure"/> is
    /// <see langword="null"/>.
    /// </exception>
    public static IHostApplicationBuilder AddAutoContextEngine(
        this IHostApplicationBuilder builder,
        Action<EngineOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddOptions<EngineOptions>()
            .Configure(configure)
            .ValidateOnStart();

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<EngineOptions>, EngineOptionsValidator>());

        // Clock source for hosted services that stamp wire-visible
        // timestamps (RegistryFileService's own-entry row et al.).
        // Idempotent — a host that already has a TimeProvider keeps
        // its own.
        builder.Services.TryAddSingleton(TimeProvider.System);

        // Registration order encodes the stop order (hosted
        // services stop in reverse). RegistryFileService is
        // registered first so it stops LAST: future hosted writers
        // (Phase 2b housekeeping, crash-writers) register after it,
        // stop before it, and can therefore await one final
        // WriteAsync through a still-live channel during their
        // own StopAsync. The file service itself owns the
        // lifecycle of this engine's own row — append on Start,
        // best-effort remove on Stop — so the writer/file-service
        // split that earlier drafts proposed is collapsed into a
        // single service.

        builder.Services.TryAddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<EngineOptions>>().Value;
            var clock = sp.GetRequiredService<TimeProvider>();
            var path = EngineCacheRoot.ResolveRegistryFilePath(options.CacheRootOverride);
            return new RegistryFileService(
                path,
                serviceOptions: null,
                readerOptions: null,
                loggerFactory: sp.GetService<Microsoft.Extensions.Logging.ILoggerFactory>(),
                ownEntryFactory: () => RegistryEntryBuilder.Build(options, clock));
        });
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, RegistryFileService>(
                sp => sp.GetRequiredService<RegistryFileService>()));

        // Stateless concurrent reader used by the RPC dispatcher's
        // Engine.RegistryEntries handler. Bound to the same
        // resolved registry path as the writer so the two see the
        // same file.
        builder.Services.TryAddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<EngineOptions>>().Value;
            var path = EngineCacheRoot.ResolveRegistryFilePath(options.CacheRootOverride);
            return new RegistryFileReader(
                path,
                options: null,
                logger: sp.GetService<Microsoft.Extensions.Logging.ILogger<RegistryFileReader>>());
        });

        builder.Services.TryAddSingleton<LifecycleEventStream>();
        builder.Services.TryAddSingleton<LifecycleNotifier>();

        // Idle-timeout watchdog: registered as a singleton (so
        // LifecycleService can inject it directly for keep-alive
        // accounting) and as an IHostedService (so it arms its
        // countdown on host start and disarms on host stop).
        // Registered BEFORE LifecycleService so it stops AFTER it
        // — LifecycleService's StopAsync tears down accept loops
        // first, then the watchdog cancels its timer.
        builder.Services.TryAddSingleton<IdleTimeoutWatchdog>();
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, IdleTimeoutWatchdog>(
                sp => sp.GetRequiredService<IdleTimeoutWatchdog>()));

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, LifecycleService>());

        return builder;
    }
}
