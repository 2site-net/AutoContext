namespace AutoContext.Engine.Core.Features.Instructions;

using System.Collections.Generic;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Core.Workspace.Config;
using AutoContext.Engine.Core.Workspace.Config.Snapshot;
using AutoContext.Engine.Protocol.Messages.Instructions;

using Microsoft.Extensions.Hosting;

/// <summary>
/// Hosted service that primes the <c>Instructions.Subscribe</c>
/// fan-out broadcaster with the current corpus listing at engine
/// start, so the snapshot-on-subscribe frame reflects the loaded
/// state before the first <c>events</c>-pipe connection can land.
/// Registered after the manifest, override, and config services so
/// the projection it primes reads fully loaded accessors. It then
/// bridges config changes into the broadcaster: each
/// <c>Config.Toggle*</c> edit (or watcher-driven reconciliation)
/// re-projects the listing — re-evaluating every row's
/// <c>disabled</c> flag against the new snapshot, with no corpus
/// reload — and republishes it, so live subscribers observe the
/// toggle and later subscribers are seeded with it.
/// </summary>
internal sealed class InstructionsSubscriptionService : IHostedService
{
    private readonly SnapshotBroadcaster<IReadOnlyList<JsonInstructionsListRow>> _broadcaster;
    private readonly IConfigChangeNotifier _configChanges;
    private readonly InstructionsListProjector _projector;

    /// <summary>
    /// Creates a new <see cref="InstructionsSubscriptionService"/>.
    /// </summary>
    /// <param name="projector">Projects the corpus listing primed as
    /// the snapshot-on-subscribe seed and republished on each config
    /// change.</param>
    /// <param name="broadcaster">Fan-out broadcaster backing the
    /// <c>Instructions.Subscribe</c> RPC stream.</param>
    /// <param name="configChanges">Config change-notification seam the
    /// rebroadcast bridge subscribes to.</param>
    /// <exception cref="ArgumentNullException">
    /// Any argument is <see langword="null"/>.
    /// </exception>
    public InstructionsSubscriptionService(
        InstructionsListProjector projector,
        SnapshotBroadcaster<IReadOnlyList<JsonInstructionsListRow>> broadcaster,
        IConfigChangeNotifier configChanges)
    {
        ArgumentNullException.ThrowIfNull(projector);
        ArgumentNullException.ThrowIfNull(broadcaster);
        ArgumentNullException.ThrowIfNull(configChanges);

        _projector = projector;
        _broadcaster = broadcaster;
        _configChanges = configChanges;
    }

    /// <summary>
    /// Subscribes to config changes, then projects the current corpus
    /// listing and primes the broadcaster's snapshot-on-subscribe
    /// seed. Subscribing first is safe: the initial config load
    /// raises no change event, so no duplicate seed is published.
    /// </summary>
    /// <param name="cancellationToken">Ignored; the projection reads
    /// in-memory accessors.</param>
    /// <returns>A completed task.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _configChanges.Changed += OnConfigChanged;
        _broadcaster.Prime(_projector.ProjectAll());
        return Task.CompletedTask;
    }

    /// <summary>
    /// Unsubscribes from config changes and completes the broadcaster
    /// so active <c>Instructions.Subscribe</c> streams observe a clean
    /// EOF.
    /// </summary>
    /// <param name="cancellationToken">Ignored.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _configChanges.Changed -= OnConfigChanged;
        _broadcaster.Complete();
        return Task.CompletedTask;
    }

    private void OnConfigChanged(object? sender, ConfigSnapshot snapshot)
        => _broadcaster.TryPublish(_projector.ProjectAll());
}
