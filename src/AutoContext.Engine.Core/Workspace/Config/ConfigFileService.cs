namespace AutoContext.Engine.Core.Workspace.Config;

using Microsoft.Extensions.Hosting;

/// <summary>
/// Hosted service that brings the workspace's
/// <see cref="ConfigFileManager"/> online at engine start:
/// it performs the initial disk load so the in-memory snapshot is
/// populated before the first <c>Config.Get</c> RPC can land, then
/// arms the file watcher so later external edits flow into the
/// snapshot. The manager singleton owns its own teardown (it is an
/// <see cref="IDisposable"/> the container disposes on host stop),
/// so <see cref="StopAsync(CancellationToken)"/> is a no-op.
/// </summary>
internal sealed class ConfigFileService : IHostedService
{
    private readonly ConfigFileManager _manager;

    /// <summary>
    /// Creates a new <see cref="ConfigFileService"/>.
    /// </summary>
    /// <param name="manager">Config manager whose snapshot this
    /// service loads and whose watcher it arms.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="manager"/> is <see langword="null"/>.
    /// </exception>
    public ConfigFileService(ConfigFileManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);

        _manager = manager;
    }

    /// <summary>
    /// Loads the snapshot from disk, then begins watching the
    /// config file for external edits.
    /// </summary>
    /// <param name="cancellationToken">Cancels the initial
    /// load.</param>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _manager.LoadAsync(cancellationToken).ConfigureAwait(false);
        _manager.Watch();
    }

    /// <summary>No-op — the manager disposes itself on host stop.</summary>
    /// <param name="cancellationToken">Ignored.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
