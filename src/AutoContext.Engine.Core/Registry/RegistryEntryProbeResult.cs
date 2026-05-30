namespace AutoContext.Engine.Core.Registry;

using AutoContext.Engine.Protocol.Messages.Registry;

/// <summary>
/// A <see cref="JsonRegistryEntry"/> paired with the liveness verdict
/// <see cref="RegistryEntryReader"/> derived from a
/// <see cref="Infrastructure.Diagnostics.IProcessLookup"/> probe.
/// Carrying the original entry alongside the verdict lets
/// downstream consumers (<c>CacheRootScanner</c>,
/// <c>StaleSubtreeCleaner</c>, and <c>RetentionPolicy</c>) reach
/// the entry's workspace hash, instance id, and per-entry
/// retention window without a second read.
/// </summary>
/// <param name="Entry">The registry entry as read from
/// <c>engine-registry.json</c>.</param>
/// <param name="State">Whether the entry's owning process is live
/// or stale (crashed/recycled/exited-without-cleanup).</param>
internal sealed record RegistryEntryProbeResult(
    JsonRegistryEntry Entry,
    RegistryEntryProbeState State);
