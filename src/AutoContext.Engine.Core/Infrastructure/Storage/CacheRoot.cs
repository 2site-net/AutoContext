namespace AutoContext.Engine.Core.Infrastructure.Storage;

using AutoContext.Engine.Core;
using AutoContext.Engine.Core.Machine;

using Microsoft.Extensions.Options;

/// <summary>
/// Per-instance identity bundle: captures the user-supplied
/// workspace path, the engine's instance id, and the derived
/// cache-root subtree the engine owns on disk
/// (<c>&lt;fullPath&gt;/&lt;workspaceHash&gt;/&lt;instanceId&gt;</c>).
/// </summary>
/// <remarks>
/// <para>
/// Constructed once at host startup from
/// <see cref="EngineOptions"/> and registered as a DI singleton.
/// Every consumer that needs a path under the engine cache root
/// — <see cref="EngineCacheLayout"/>, the registry file service,
/// the cache-root scanner — composes off this instance rather
/// than re-resolving from <see cref="EngineOptions"/>.
/// </para>
/// <para>
/// Naming: <see cref="WorkspaceUserPath"/> preserves the original
/// input verbatim (so crash records, registry rows, and log
/// scopes report what the caller actually passed in);
/// <see cref="WorkspaceBucketPath"/> is the per-workspace bucket
/// directory under the cache root that holds every concurrent
/// engine instance for that workspace; <see cref="InstancePath"/>
/// is this engine's own subtree under that bucket.
/// </para>
/// </remarks>
public sealed record class CacheRoot
{
    /// <summary>
    /// Creates a new <see cref="CacheRoot"/> from
    /// <paramref name="options"/>. The OS cache root is resolved
    /// eagerly via <see cref="CacheRootPathResolver"/> so every
    /// downstream consumer sees the same frozen value.
    /// </summary>
    /// <param name="options">Engine options carrying the
    /// workspace path, instance id, and optional cache-root
    /// override.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    public CacheRoot(IOptions<EngineOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var value = options.Value;

        InstanceId = value.InstanceId.ToString("D");
        WorkspaceUserPath = value.WorkspacePath;

        FullPath = CacheRootPathResolver.Resolve(value.CacheRootOverride);
        WorkspaceHash = Protocol.WorkspaceHash.Compute(WorkspaceUserPath).Value;

        WorkspaceBucketPath = Path.Combine(
            FullPath,
            WorkspaceHash);

        InstancePath = Path.Combine(
            WorkspaceBucketPath,
            InstanceId);
    }

    /// <summary>
    /// Absolute path to the OS-level engine cache root
    /// (e.g. <c>%LOCALAPPDATA%\autocontext\</c> on Windows,
    /// <c>$XDG_CACHE_HOME/autocontext/</c> on POSIX).
    /// Shared by every engine on the same user account.
    /// </summary>
    public string FullPath { get; }

    /// <summary>
    /// Engine instance id in canonical "D" form, used as the
    /// per-instance directory name under <see cref="WorkspaceBucketPath"/>.
    /// </summary>
    public string InstanceId { get; }

    /// <summary>
    /// Absolute path to this engine instance's own subtree
    /// (<c>&lt;fullPath&gt;/&lt;workspaceHash&gt;/&lt;instanceId&gt;</c>).
    /// All per-instance artefacts (logs, crash tombstones) live
    /// under this directory.
    /// </summary>
    public string InstancePath { get; }

    /// <summary>
    /// Absolute path to the per-workspace bucket directory
    /// (<c>&lt;fullPath&gt;/&lt;workspaceHash&gt;</c>). Every
    /// concurrent engine instance for the same workspace lives
    /// in its own <see cref="InstancePath"/> subdirectory under
    /// this bucket.
    /// </summary>
    public string WorkspaceBucketPath { get; }

    /// <summary>
    /// Stable hash of <see cref="WorkspaceUserPath"/> used to
    /// partition the cache root by workspace. Two engines on the
    /// same workspace share this value; two engines on different
    /// workspaces do not.
    /// </summary>
    public string WorkspaceHash { get; }

    /// <summary>
    /// Original workspace path supplied by the caller via
    /// <see cref="EngineOptions.WorkspacePath"/>. Preserved
    /// verbatim — crash records, registry rows, and log scopes
    /// echo back this value, not the derived
    /// <see cref="WorkspaceBucketPath"/> bucket.
    /// </summary>
    public string WorkspaceUserPath { get; }
}
