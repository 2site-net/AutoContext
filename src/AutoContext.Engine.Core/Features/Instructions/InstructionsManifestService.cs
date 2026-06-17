namespace AutoContext.Engine.Core.Features.Instructions;

using AutoContext.Engine.Core.Features.Instructions.Snapshot;
using AutoContext.Engine.Core.Infrastructure;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Hosted service that loads the bundled instructions corpus at engine
/// start and holds the immutable snapshot every <c>Instructions.*</c>
/// RPC handler reads through the
/// <see cref="IInstructionsManifestAccessor"/> seam. The corpus is
/// shipped beside the engine binary as two build-time side-cars (the
/// generated manifest and the hand-authored catalog) and never changes at
/// runtime, so the load is a one-shot read in
/// <see cref="StartAsync(CancellationToken)"/>; there is no watcher and
/// <see cref="StopAsync(CancellationToken)"/> has nothing to tear down.
/// Registered before the RPC pipes so the snapshot is populated before
/// the first connection can land.
/// </summary>
internal sealed partial class InstructionsManifestService : IHostedService, IInstructionsManifestAccessor
{
    private readonly ILogger<InstructionsManifestService> _logger;
    private readonly EngineResourcesDirectory _resources;
    private volatile InstructionsManifestSnapshot _current = InstructionsManifestSnapshot.Empty;

    /// <summary>
    /// Creates a service that loads the corpus from
    /// <paramref name="resources"/>.
    /// </summary>
    /// <param name="resources">The resources directory holding the two
    /// side-cars (override copies shadow the bundled ones). Must not be
    /// <see langword="null"/>.</param>
    /// <param name="logger">Diagnostic sink.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="resources"/> or <paramref name="logger"/> is
    /// <see langword="null"/>.</exception>
    public InstructionsManifestService(
        EngineResourcesDirectory resources,
        ILogger<InstructionsManifestService> logger)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(logger);

        _resources = resources;
        _logger = logger;
    }

    /// <inheritdoc />
    public InstructionsManifestSnapshot Current => _current;

    /// <summary>
    /// Loads the corpus snapshot from the side-cars and publishes it to
    /// <see cref="Current"/>. Throws — failing host start — when the
    /// bundled side-cars are missing or malformed.
    /// </summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var snapshot = await InstructionsManifestLoader
            .LoadAsync(_resources, cancellationToken)
            .ConfigureAwait(false);

        _current = snapshot;

        if (_resources.OverrideDirectory is { } overrideRoot)
        {
            LogCorpusLoadedWithOverride(_logger, snapshot.Files.Count, _resources.BaseDirectory, overrideRoot);
        }
        else
        {
            LogCorpusLoaded(_logger, snapshot.Files.Count, _resources.BaseDirectory);
        }
    }

    /// <summary>
    /// No-op: the corpus is read-only in-memory state with nothing to
    /// release.
    /// </summary>
    /// <param name="cancellationToken">Ignored.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Loaded instruction corpus: {Count} files from '{ResourcesDirectory}'.")]
    private static partial void LogCorpusLoaded(ILogger logger, int count, string resourcesDirectory);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Loaded instruction corpus: {Count} files from '{ResourcesDirectory}' "
            + "with side-car overrides from '{OverrideDirectory}'.")]
    private static partial void LogCorpusLoadedWithOverride(
        ILogger logger,
        int count,
        string resourcesDirectory,
        string overrideDirectory);
}
