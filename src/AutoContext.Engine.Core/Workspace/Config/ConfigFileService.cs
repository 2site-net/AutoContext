namespace AutoContext.Engine.Core.Workspace.Config;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;
using AutoContext.Engine.Protocol.Messages.Config;

using Microsoft.Extensions.Hosting;

/// <summary>
/// Hosted service that brings the workspace's
/// <see cref="ConfigFileManager"/> online at engine start and
/// bridges its change events into the
/// <see cref="SnapshotBroadcaster{T}"/>: it subscribes to the
/// manager's change event, performs the initial disk load so the
/// in-memory snapshot is populated before the first <c>Config.Get</c>
/// RPC can land, primes the broadcaster with that loaded snapshot
/// (the load itself raises no change event), then arms the file
/// watcher so later external edits flow into both the snapshot and
/// every <c>Config.Subscribe</c> stream. The manager singleton owns
/// its own teardown (it is an <see cref="IDisposable"/> the container
/// disposes on host stop), so
/// <see cref="StopAsync(CancellationToken)"/> only unsubscribes and
/// completes the broadcaster.
/// </summary>
internal sealed class ConfigFileService : IHostedService
{
    private readonly SnapshotBroadcaster<JsonConfigSnapshot> _broadcaster;
    private readonly ConfigFileManager _manager;

    /// <summary>
    /// Creates a new <see cref="ConfigFileService"/>.
    /// </summary>
    /// <param name="manager">Config manager whose snapshot this
    /// service loads and whose watcher it arms.</param>
    /// <param name="broadcaster">Fan-out broadcaster the manager's
    /// change events are bridged into for <c>Config.Subscribe</c>.</param>
    /// <exception cref="ArgumentNullException">
    /// Any argument is <see langword="null"/>.
    /// </exception>
    public ConfigFileService(
        ConfigFileManager manager,
        SnapshotBroadcaster<JsonConfigSnapshot> broadcaster)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(broadcaster);

        _manager = manager;
        _broadcaster = broadcaster;
    }

    /// <summary>
    /// Subscribes to the manager's change event, loads the snapshot
    /// from disk, primes the broadcaster's snapshot-on-subscribe
    /// seed, then begins watching the config file for external
    /// edits.
    /// </summary>
    /// <param name="cancellationToken">Cancels the initial
    /// load.</param>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Subscribe BEFORE arming the watcher so no watcher-driven
        // change can slip past between the load and the
        // subscription. LoadAsync itself raises no change event, so
        // subscribing ahead of it is safe — no duplicate seed.
        _manager.Changed += OnConfigChanged;

        var initial = await _manager.LoadAsync(cancellationToken).ConfigureAwait(false);
        _broadcaster.Prime(initial.ToWireFormat());

        _manager.Watch();
    }

    /// <summary>
    /// Unsubscribes from the manager and completes the broadcaster
    /// so active <c>Config.Subscribe</c> streams observe a clean EOF.
    /// </summary>
    /// <param name="cancellationToken">Ignored.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _manager.Changed -= OnConfigChanged;
        _broadcaster.Complete();
        return Task.CompletedTask;
    }

    private void OnConfigChanged(object? sender, ConfigSnapshot snapshot)
        => _broadcaster.TryPublish(snapshot.ToWireFormat());
}
