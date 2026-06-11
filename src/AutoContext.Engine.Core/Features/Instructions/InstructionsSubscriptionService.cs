namespace AutoContext.Engine.Core.Features.Instructions;

using System.Collections.Generic;

using AutoContext.Engine.Core.Infrastructure.Events;
using AutoContext.Engine.Protocol.Messages.Instructions;

using Microsoft.Extensions.Hosting;

/// <summary>
/// Hosted service that primes the <c>Instructions.Subscribe</c>
/// fan-out broadcaster with the current corpus listing at engine
/// start, so the snapshot-on-subscribe frame reflects the loaded
/// state before the first <c>events</c>-pipe connection can land.
/// Registered after the manifest, override, and config services so
/// the projection it primes reads fully loaded accessors. The
/// listing is static for this increment; republishing on corpus
/// reload arrives with the config-change rebroadcast bridge.
/// </summary>
internal sealed class InstructionsSubscriptionService : IHostedService
{
    private readonly SnapshotBroadcaster<IReadOnlyList<JsonInstructionsListRow>> _broadcaster;
    private readonly InstructionsListProjector _projector;

    /// <summary>
    /// Creates a new <see cref="InstructionsSubscriptionService"/>.
    /// </summary>
    /// <param name="projector">Projects the corpus listing primed as
    /// the snapshot-on-subscribe seed.</param>
    /// <param name="broadcaster">Fan-out broadcaster backing the
    /// <c>Instructions.Subscribe</c> RPC stream.</param>
    /// <exception cref="ArgumentNullException">
    /// Any argument is <see langword="null"/>.
    /// </exception>
    public InstructionsSubscriptionService(
        InstructionsListProjector projector,
        SnapshotBroadcaster<IReadOnlyList<JsonInstructionsListRow>> broadcaster)
    {
        ArgumentNullException.ThrowIfNull(projector);
        ArgumentNullException.ThrowIfNull(broadcaster);

        _projector = projector;
        _broadcaster = broadcaster;
    }

    /// <summary>
    /// Projects the current corpus listing and primes the
    /// broadcaster's snapshot-on-subscribe seed.
    /// </summary>
    /// <param name="cancellationToken">Ignored; the projection reads
    /// in-memory accessors.</param>
    /// <returns>A completed task.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _broadcaster.Prime(_projector.ProjectAll());
        return Task.CompletedTask;
    }

    /// <summary>
    /// Completes the broadcaster so active
    /// <c>Instructions.Subscribe</c> streams observe a clean EOF.
    /// </summary>
    /// <param name="cancellationToken">Ignored.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _broadcaster.Complete();
        return Task.CompletedTask;
    }
}
