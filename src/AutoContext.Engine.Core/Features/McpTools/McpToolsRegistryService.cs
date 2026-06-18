namespace AutoContext.Engine.Core.Features.McpTools;

using AutoContext.Engine.Core.Features.McpTools.Snapshot;
using AutoContext.Engine.Core.Infrastructure;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Hosted service that loads the bundled MCP-tools registry at engine
/// start and holds the immutable snapshot every <c>McpTools.*</c> RPC
/// handler reads through the <see cref="IMcpToolsRegistryAccessor"/>
/// seam. The registry is shipped beside the engine binary as build-time
/// side-cars (the hand-authored execution registry and UI catalog, each
/// with its schema) and never changes at runtime, so the load is a
/// one-shot read in <see cref="StartAsync(CancellationToken)"/>; there is
/// no watcher and <see cref="StopAsync(CancellationToken)"/> has nothing
/// to tear down. Registered before the RPC pipes so the snapshot is
/// populated before the first connection can land.
/// </summary>
internal sealed partial class McpToolsRegistryService : IHostedService, IMcpToolsRegistryAccessor
{
    private readonly ILogger<McpToolsRegistryService> _logger;
    private readonly EngineResourcesDirectory _resources;
    private volatile McpToolsRegistry _current = McpToolsRegistry.Empty;

    /// <summary>
    /// Creates a service that loads the registry from
    /// <paramref name="resources"/>.
    /// </summary>
    /// <param name="resources">The resources directory holding the
    /// side-cars (override copies shadow the bundled ones). Must not be
    /// <see langword="null"/>.</param>
    /// <param name="logger">Diagnostic sink.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="resources"/> or <paramref name="logger"/> is
    /// <see langword="null"/>.</exception>
    public McpToolsRegistryService(
        EngineResourcesDirectory resources,
        ILogger<McpToolsRegistryService> logger)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(logger);

        _resources = resources;
        _logger = logger;
    }

    /// <inheritdoc />
    public McpToolsRegistry Current => _current;

    /// <summary>
    /// Loads the registry snapshot from the side-cars and publishes it to
    /// <see cref="Current"/>. Throws — failing host start — when the
    /// bundled side-cars are missing or malformed.
    /// </summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var registry = await McpToolsRegistryLoader
            .LoadAsync(_resources, cancellationToken)
            .ConfigureAwait(false);

        _current = registry;

        if (_resources.OverrideDirectory is { } overrideRoot)
        {
            LogRegistryLoadedWithOverride(_logger, registry.Tools.Count, _resources.BaseDirectory, overrideRoot);
        }
        else
        {
            LogRegistryLoaded(_logger, registry.Tools.Count, _resources.BaseDirectory);
        }
    }

    /// <summary>
    /// No-op: the registry is read-only in-memory state with nothing to
    /// release.
    /// </summary>
    /// <param name="cancellationToken">Ignored.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Loaded MCP-tools registry: {Count} tools from '{ResourcesDirectory}'.")]
    private static partial void LogRegistryLoaded(ILogger logger, int count, string resourcesDirectory);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Loaded MCP-tools registry: {Count} tools from '{ResourcesDirectory}' "
            + "with side-car overrides from '{OverrideDirectory}'.")]
    private static partial void LogRegistryLoadedWithOverride(
        ILogger logger,
        int count,
        string resourcesDirectory,
        string overrideDirectory);
}
