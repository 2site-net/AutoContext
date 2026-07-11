namespace AutoContext.Engine.Core.Machine;

using AutoContext.Engine.Core.Infrastructure.Storage;

/// <summary>
/// Single source of truth for every on-disk path the engine owns
/// under its cache root — the per-instance logs and crash
/// tombstone, plus the shared liveness registry file at the
/// cache-root level. Consumers compose against this layout rather
/// than re-resolving directories or concatenating basenames.
/// </summary>
/// <remarks>
/// <para>
/// All paths are computed eagerly in the constructor from the
/// injected <see cref="Infrastructure.Storage.CacheRoot"/> and frozen thereafter.
/// New on-disk artefacts owned by the engine should be added here
/// (basename constant + resolved property) so the layout stays
/// the one place a reader looks to learn where the engine writes.
/// </para>
/// </remarks>
public sealed class EngineCacheLayout
{
    /// <summary>Basename of the per-instance crash tombstone.</summary>
    public const string CrashLogFileName = "crash.log";

    /// <summary>Basename of the active engine log file.</summary>
    public const string EngineLogFileName = "engine.log";

    /// <summary>Name of the per-instance logs directory.</summary>
    public const string LogsDirName = "logs";

    /// <summary>Basename of the shared liveness registry file.</summary>
    public const string RegistryFileName = "engine-registry.json";

    /// <summary>
    /// Filename prefix for per-worker log files. A worker's active
    /// log file is <c>worker-&lt;workerId&gt;.log</c> and its
    /// rotated siblings are
    /// <c>worker-&lt;workerId&gt;-&lt;timestamp&gt;.log</c>.
    /// </summary>
    public const string WorkerLogFilePrefix = "worker-";

    /// <summary>
    /// Creates a new <see cref="EngineCacheLayout"/> rooted at the
    /// per-instance subtree described by <paramref name="cacheRoot"/>.
    /// </summary>
    /// <param name="cacheRoot">Resolved per-instance identity and
    /// derivations that every path below is composed from.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="cacheRoot"/> is <see langword="null"/>.
    /// </exception>
    public EngineCacheLayout(CacheRoot cacheRoot)
    {
        ArgumentNullException.ThrowIfNull(cacheRoot);

        CacheRoot = cacheRoot;

        LogsDirPath = Path.Combine(
            cacheRoot.InstancePath,
            LogsDirName);

        CrashLogFilePath = Path.Combine(
            LogsDirPath,
            CrashLogFileName);

        EngineLogFilePath = Path.Combine(
            LogsDirPath,
            EngineLogFileName);

        RegistryFilePath = Path.Combine(
            cacheRoot.FullPath,
            RegistryFileName);
    }

    /// <summary>
    /// The per-instance identity bundle these paths were composed
    /// from. Exposed so consumers that need both raw identity
    /// (e.g. crash records embedding <see cref="CacheRoot.WorkspaceUserPath"/>)
    /// and resolved paths can take a single dependency.
    /// </summary>
    public CacheRoot CacheRoot { get; }

    /// <summary>
    /// Absolute path to the per-instance <c>crash.log</c>
    /// tombstone under <see cref="LogsDirPath"/>.
    /// </summary>
    public string CrashLogFilePath { get; }

    /// <summary>
    /// Absolute path to the active engine log file
    /// (<c>engine.log</c>) under the per-instance subtree.
    /// </summary>
    public string EngineLogFilePath { get; }

    /// <summary>
    /// Absolute path to the engine's <c>logs/</c> directory under
    /// the per-instance subtree. Rotated engine log files sit
    /// beside <see cref="EngineLogFilePath"/> in this directory.
    /// </summary>
    public string LogsDirPath { get; }

    /// <summary>
    /// Absolute path to the shared liveness registry file at the
    /// engine cache-root level (one file per user account, shared
    /// across every concurrent engine instance).
    /// </summary>
    public string RegistryFilePath { get; }

    /// <summary>
    /// Rotation basename for a worker's log files —
    /// <c>worker-&lt;workerId&gt;</c>. The active file appends
    /// <c>.log</c>; rotated siblings append
    /// <c>-&lt;timestamp&gt;.log</c>.
    /// </summary>
    /// <param name="workerId">Identifier of the worker whose logs
    /// this basename groups. Must be non-empty.</param>
    /// <returns>The rotation basename.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="workerId"/> is <see langword="null"/> or
    /// empty.
    /// </exception>
    public static string WorkerLogBaseName(string workerId)
    {
        ArgumentException.ThrowIfNullOrEmpty(workerId);
        return WorkerLogFilePrefix + workerId;
    }

    /// <summary>
    /// Absolute path to a worker's active log file
    /// (<c>worker-&lt;workerId&gt;.log</c>) under the per-instance
    /// <see cref="LogsDirPath"/>. Records whose
    /// <c>category</c> begins <c>worker.&lt;workerId&gt;.</c> land
    /// here; every other record lands in
    /// <see cref="EngineLogFilePath"/>.
    /// </summary>
    /// <param name="workerId">Identifier of the worker whose log
    /// file to resolve. Must be non-empty.</param>
    /// <returns>The absolute path to the worker's active log file.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="workerId"/> is <see langword="null"/> or
    /// empty.
    /// </exception>
    public string WorkerLogFilePath(string workerId)
    {
        ArgumentException.ThrowIfNullOrEmpty(workerId);
        return Path.Combine(LogsDirPath, WorkerLogBaseName(workerId) + ".log");
    }
}
