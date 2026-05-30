namespace AutoContext.Engine.Core.Machine.Housekeeping;

using AutoContext.Engine.Protocol.Messages.Registry;

/// <summary>
/// Closed-set classification of a single on-disk subtree under the
/// engine cache root against the shared liveness registry. The
/// four arms — <see cref="Registered"/>, <see cref="StaleRegistration"/>,
/// <see cref="Unregistered"/>, <see cref="Foreign"/> — are the
/// contract between <see cref="CacheRootScanner"/> (which
/// produces this), <c>RetentionPolicy</c> (which resolves a
/// deletion window per arm), and <c>StaleSubtreeCleaner</c>
/// (which pattern-matches to act). Promoting the classification
/// from an internal switch to a public-shape type lets each
/// consumer be tested in isolation and gives per-arm diagnostics
/// ("reaped N stale-registered, M foreign") for free.
/// </summary>
/// <remarks>
/// <para>
/// Every arm carries <see cref="SubtreePath"/> — the absolute
/// directory path the classification applies to — because that
/// is the input every downstream consumer needs and the only piece
/// of data the scanner has on a <see cref="Foreign"/> subtree.
/// Arms that match a registry entry (<see cref="Registered"/>,
/// <see cref="StaleRegistration"/>) carry the matched
/// <see cref="JsonRegistryEntry"/> so the cleaner can honour the
/// entry's own retention without a second registry read.
/// </para>
/// </remarks>
/// <param name="SubtreePath">Absolute path of the classified
/// directory on disk.</param>
internal abstract record SubtreeRegistryStatus(string SubtreePath)
{
    /// <summary>
    /// Anything under the cache root that doesn't match the
    /// canonical nested per-instance shape: legacy flat
    /// <c>&lt;workspaceHash&gt;#&lt;instanceId&gt;</c> directories
    /// from before the nested layout, bare
    /// <c>&lt;workspaceHash&gt;</c> directories from even earlier
    /// preview builds, or any other directory whose name does not
    /// parse as a <c>WorkspaceHash</c> or
    /// (under one) a <c>Guid</c>. By definition stale; eligible
    /// for cleanup under this engine's <c>--retention</c> floor.
    /// </summary>
    /// <param name="SubtreePath">Absolute path of the foreign
    /// subtree.</param>
    internal sealed record Foreign(string SubtreePath)
        : SubtreeRegistryStatus(SubtreePath);

    /// <summary>
    /// Canonical <c>&lt;workspaceHash&gt;/&lt;instanceId&gt;</c>
    /// subtree backed by a registry entry whose owning process is
    /// live (pid resolves and <see cref="System.Diagnostics.Process.StartTime"/>
    /// agrees, defeating pid recycling). Must not be touched by
    /// housekeeping.
    /// </summary>
    /// <param name="SubtreePath">Absolute path of the live
    /// per-instance subtree.</param>
    /// <param name="Entry">The matched registry entry.</param>
    internal sealed record Registered(
        string SubtreePath,
        JsonRegistryEntry Entry) : SubtreeRegistryStatus(SubtreePath);

    /// <summary>
    /// Canonical <c>&lt;workspaceHash&gt;/&lt;instanceId&gt;</c>
    /// subtree backed by a registry entry whose owning process is
    /// dead or recycled. Eligible for cleanup once outside the
    /// entry's own retention window.
    /// </summary>
    /// <param name="SubtreePath">Absolute path of the stale
    /// per-instance subtree.</param>
    /// <param name="Entry">The stale registry entry — carried so
    /// the cleaner honours
    /// <see cref="JsonRegistryEntry.Retention"/>.</param>
    internal sealed record StaleRegistration(
        string SubtreePath,
        JsonRegistryEntry Entry) : SubtreeRegistryStatus(SubtreePath);

    /// <summary>
    /// Canonical <c>&lt;workspaceHash&gt;/&lt;instanceId&gt;</c>
    /// subtree with no registry entry claiming it (the owning
    /// engine crashed before writing its row, or its row was
    /// already reaped by a peer). Eligible for cleanup under this
    /// engine's <c>--retention</c> floor.
    /// </summary>
    /// <param name="SubtreePath">Absolute path of the orphan
    /// per-instance subtree.</param>
    internal sealed record Unregistered(string SubtreePath)
        : SubtreeRegistryStatus(SubtreePath);
}
