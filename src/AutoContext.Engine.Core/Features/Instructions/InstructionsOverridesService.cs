namespace AutoContext.Engine.Core.Features.Instructions;

using AutoContext.Engine.Core.Features.Instructions.Snapshot;
using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Context;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Hosted service that performs the one-shot startup scan of the
/// workspace's instructions override directories and exposes the resulting
/// inventory through the <see cref="IInstructionsOverridesAccessor"/> seam
/// that every <c>Instructions.*</c> RPC handler reads. The override roots
/// are taken from the resolved engine settings
/// (<c>InstructionsOverridesRoots</c>), so this service must start after
/// the configuration is loaded.
/// </summary>
/// <remarks>
/// This wrapper deliberately performs a load-only scan: it constructs an
/// <see cref="InstructionsOverridesWatcher"/> and calls
/// <see cref="InstructionsOverridesWatcher.LoadAsync"/> in
/// <see cref="StartAsync(CancellationToken)"/> but never arms the
/// filesystem watcher, so the inventory reflects the override files present
/// at engine start and does not change at runtime. Live re-scanning on
/// external edits is a later increment. When the workspace path is unknown
/// the service stays at <see cref="InstructionsOverridesSnapshot.Empty"/>.
/// </remarks>
internal sealed partial class InstructionsOverridesService
    : IHostedService, IInstructionsOverridesAccessor, IDisposable
{
    private static readonly IReadOnlyList<string> DefaultOverrideRoots = [".github"];

    private readonly IConfigSnapshotAccessor _configAccessor;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<InstructionsOverridesService> _logger;
    private readonly StaleOverrideInspector _staleOverrideInspector;
    private readonly TimeProvider _timeProvider;
    private readonly IWorkspaceContextAccessor _workspaceAccessor;
    private InstructionsOverridesWatcher? _watcher;

    /// <summary>
    /// Creates the hosted override scanner.
    /// </summary>
    /// <param name="workspaceAccessor">Supplies the resolved workspace
    /// folder the override roots are anchored to.</param>
    /// <param name="configAccessor">Supplies the configured override
    /// roots.</param>
    /// <param name="staleOverrideInspector">Warns when a scanned override
    /// is older than the bundled file it shadows.</param>
    /// <param name="timeProvider">Clock forwarded to the watcher.</param>
    /// <param name="loggerFactory">Creates the watcher's logger.</param>
    /// <param name="logger">Diagnostic sink for this service.</param>
    /// <exception cref="ArgumentNullException">Any argument is
    /// <see langword="null"/>.</exception>
    public InstructionsOverridesService(
        IWorkspaceContextAccessor workspaceAccessor,
        IConfigSnapshotAccessor configAccessor,
        StaleOverrideInspector staleOverrideInspector,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory,
        ILogger<InstructionsOverridesService> logger)
    {
        ArgumentNullException.ThrowIfNull(workspaceAccessor);
        ArgumentNullException.ThrowIfNull(configAccessor);
        ArgumentNullException.ThrowIfNull(staleOverrideInspector);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _workspaceAccessor = workspaceAccessor;
        _configAccessor = configAccessor;
        _staleOverrideInspector = staleOverrideInspector;
        _timeProvider = timeProvider;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public InstructionsOverridesSnapshot Current =>
        _watcher?.Current ?? InstructionsOverridesSnapshot.Empty;

    /// <summary>
    /// Scans the override directories once and publishes the inventory to
    /// <see cref="Current"/>. Stays empty when the workspace path is
    /// unknown.
    /// </summary>
    /// <param name="cancellationToken">Cancels the scan.</param>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var workspacePath = _workspaceAccessor.EngineInfo.WorkspacePath;

        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            LogNoWorkspace(_logger);
            return;
        }

        var roots = _configAccessor.Current.Engine?.InstructionsOverridesRoots is { Count: > 0 } configured
            ? configured
            : DefaultOverrideRoots;

        var watcher = new InstructionsOverridesWatcher(
            workspacePath,
            roots,
            _timeProvider,
            InstructionsOverridesWatcher.DefaultDebounceDelay,
            _loggerFactory.CreateLogger<InstructionsOverridesWatcher>());

        InstructionsOverridesSnapshot snapshot;

        try
        {
            snapshot = await watcher.LoadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            watcher.Dispose();
            throw;
        }

        _watcher = watcher;

        LogOverridesLoaded(_logger, snapshot.Count, workspacePath);
        _staleOverrideInspector.Inspect(snapshot);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _watcher?.Dispose();
        _watcher = null;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Loaded {Count} instruction override(s) for workspace '{WorkspacePath}'.")]
    private static partial void LogOverridesLoaded(ILogger logger, int count, string workspacePath);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "No workspace path resolved; instruction overrides stay empty.")]
    private static partial void LogNoWorkspace(ILogger logger);
}
