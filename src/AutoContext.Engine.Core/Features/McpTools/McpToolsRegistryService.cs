namespace AutoContext.Engine.Core.Features.McpTools;

using AutoContext.Engine.Core.Features.McpTools.Snapshot;

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
    private readonly string _resourcesDirectory;
    private volatile McpToolsRegistry _current = McpToolsRegistry.Empty;

    /// <summary>
    /// Creates a service that loads the registry from
    /// <paramref name="resourcesDirectory"/>.
    /// </summary>
    /// <param name="resourcesDirectory">Absolute path of the directory
    /// holding the side-cars. Must not be <see langword="null"/>, empty,
    /// or whitespace.</param>
    /// <param name="logger">Diagnostic sink.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="resourcesDirectory"/> is <see langword="null"/>,
    /// empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="logger"/> is <see langword="null"/>.</exception>
    public McpToolsRegistryService(
        string resourcesDirectory,
        ILogger<McpToolsRegistryService> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourcesDirectory);
        ArgumentNullException.ThrowIfNull(logger);

        _resourcesDirectory = resourcesDirectory;
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
            .LoadAsync(_resourcesDirectory, cancellationToken)
            .ConfigureAwait(false);

        _current = registry;
        LogRegistryLoaded(_logger, registry.Tools.Count, _resourcesDirectory);
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
}
