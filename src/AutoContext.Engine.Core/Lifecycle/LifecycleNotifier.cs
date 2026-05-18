namespace AutoContext.Engine.Core.Lifecycle;

using AutoContext.Engine.Protocol.Messages.Lifecycle;

using Microsoft.Extensions.Options;

/// <summary>
/// Stamps the engine's identity onto lifecycle events and publishes
/// them through the singleton <see cref="LifecycleEventStream"/>.
/// Each method maps to one wire-visible
/// <see cref="LifecycleEventKinds"/> transition; the stream stays
/// agnostic of which kind is terminal and which is not.
/// </summary>
/// <remarks>
/// <para>
/// Phase 1 exposes only <see cref="NotifyShutdown"/>; the
/// reload-pipeline kinds (<c>reloading</c>, <c>reloaded</c>) land
/// here as additional methods in a later phase that bumps the
/// snapshot revision counter.
/// </para>
/// <para>
/// Centralising stamping in this class keeps the engine's identity
/// (<see cref="EngineOptions.InstanceId"/>) and the lifecycle
/// revision counter authored in one place — the stream itself never
/// constructs <see cref="LifecycleEvent"/> values for transitions
/// other than the seeded <c>started</c> event.
/// </para>
/// </remarks>
internal sealed class LifecycleNotifier
{
    private readonly LifecycleEventStream _events;
    private readonly Guid _instanceId;

    /// <summary>
    /// Creates a new <see cref="LifecycleNotifier"/>.
    /// </summary>
    /// <param name="events">Singleton fan-out stream the notifier
    /// publishes through.</param>
    /// <param name="options">Engine options — used to stamp
    /// <see cref="LifecycleEvent.InstanceId"/> onto every published
    /// event.</param>
    /// <exception cref="ArgumentNullException">
    /// Any argument is <see langword="null"/>.
    /// </exception>
    public LifecycleNotifier(
        LifecycleEventStream events,
        IOptions<EngineOptions> options)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(options);

        _events = events;
        _instanceId = options.Value.InstanceId;
    }

    /// <summary>
    /// Publishes the terminal
    /// <see cref="LifecycleEventKinds.ShuttingDown"/> event and
    /// completes the stream. Idempotent: subsequent calls return
    /// <see langword="false"/>.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if this call completed the stream;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool NotifyShutdown()
    {
        return _events.TryComplete(new LifecycleEvent
        {
            Kind = LifecycleEventKinds.ShuttingDown,
            InstanceId = _instanceId,
            Revision = 0,
        });
    }
}
