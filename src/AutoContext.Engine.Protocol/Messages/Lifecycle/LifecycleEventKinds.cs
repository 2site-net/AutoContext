namespace AutoContext.Engine.Protocol.Messages.Lifecycle;

/// <summary>
/// Kebab-case wire-string constants for every <see cref="LifecycleEvent.Kind"/>
/// value the engine emits on the <c>events</c> pipe. Centralised
/// here so both engine and clients reference the same literals
/// without copy-paste drift. Source: <c>design § Engine.Lifecycle.Subscribe</c>.
/// </summary>
public static class LifecycleEventKinds
{
    /// <summary>
    /// Sent to every fresh subscriber immediately after the
    /// handshake completes; carries the current
    /// <c>(instanceId, revision)</c> pair so clients can prime
    /// their dedup window without an extra round-trip.
    /// </summary>
    public const string Started = "started";

    /// <summary>
    /// Broadcast at the start of a snapshot swap. The constant is
    /// reserved so client code targeting the protocol does not
    /// have to define its own; the engine does not yet emit this
    /// kind, awaiting the reload pipeline.
    /// </summary>
    public const string Reloading = "reloading";

    /// <summary>
    /// Broadcast once a snapshot swap has committed. Reserved
    /// alongside <see cref="Reloading"/>.
    /// </summary>
    public const string Reloaded = "reloaded";

    /// <summary>
    /// Broadcast when the engine begins a graceful shutdown,
    /// before the pipes are torn down, so subscribers can detach
    /// cleanly.
    /// </summary>
    public const string ShuttingDown = "shutting-down";

    /// <summary>
    /// Terminal frame the engine writes when a subscriber's
    /// bounded buffer overflows (see
    /// <c>design § events &gt; backpressure</c>).
    /// After this frame the engine completes the connection; the
    /// rest of the subscriber population keeps receiving events
    /// uninterrupted.
    /// </summary>
    public const string Evicted = "evicted";
}
