namespace AutoContext.Engine.Core.Registry;

/// <summary>
/// Liveness verdict <see cref="RegistryEntryReader"/> attaches to
/// every <see cref="Protocol.Messages.Registry.RegistryEntry"/> it
/// reads back from <c>engine-registry.json</c>. The closed-set
/// classification is consumed by <c>CacheRootScanner</c> (Phase
/// 2b row 8) as the registration half of its four-arm
/// <c>SubtreeRegistryStatus</c> output.
/// </summary>
internal enum RegistryEntryProbeState
{
    /// <summary>
    /// The entry's <see cref="Protocol.Messages.Registry.RegistryEntry.ProcessId"/>
    /// resolves to a live OS process whose start time matches the
    /// entry's <see cref="Protocol.Messages.Registry.RegistryEntry.ProcessStartTimeUtc"/>
    /// within the tolerance window. The owning engine is presumed
    /// to be running.
    /// </summary>
    Live,

    /// <summary>
    /// The entry's pid is gone, inaccessible, or has been recycled
    /// onto a different process (start times disagree). The owning
    /// engine is presumed to have crashed or exited without
    /// removing its row.
    /// </summary>
    Stale,
}
