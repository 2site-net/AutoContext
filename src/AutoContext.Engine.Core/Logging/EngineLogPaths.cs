namespace AutoContext.Engine.Core.Logging;

using AutoContext.Engine.Core.Infrastructure.Primitives;
using AutoContext.Engine.Core.Registry;

using Microsoft.Extensions.Options;

/// <summary>
/// Single source of truth for the on-disk paths the engine's
/// log pipeline owns under
/// <c>&lt;cacheRoot&gt;/&lt;workspaceHash&gt;/&lt;instanceId&gt;/logs/</c>.
/// </summary>
/// <remarks>
/// <para>
/// Both the producer side (<see cref="LogFileSinkService"/>) and
/// the consumer side (the <c>Logs.GetEngine</c> handler, and the
/// future <c>Logs.TailEngine</c> handler the Phase 3 prelude
/// introduces) resolve the active engine log path through this
/// singleton so the path is defined once. The values are computed
/// eagerly in the constructor and frozen thereafter — the
/// per-instance subtree shape is fixed once
/// <see cref="EngineOptions"/> is bound.
/// </para>
/// </remarks>
internal sealed class EngineLogPaths
{
    /// <summary>
    /// Creates a new <see cref="EngineLogPaths"/> rooted at the
    /// per-instance subtree derived from
    /// <paramref name="options"/>.
    /// </summary>
    /// <param name="options">Engine options carrying the workspace
    /// path, instance id, and optional cache-root override the
    /// resolved paths derive from.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    public EngineLogPaths(IOptions<EngineOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var value = options.Value;
        var cacheRoot = EngineCacheRoot.Resolve(value.CacheRootOverride);
        var workspaceHash = WorkspaceHash.Compute(value.WorkspacePath).Value;

        LogsDirectory = Path.Combine(
            cacheRoot,
            workspaceHash,
            value.InstanceId.ToString("D"),
            EngineCrashWriter.LogsSubdirectory);

        EngineLogFilePath = Path.Combine(
            LogsDirectory,
            LogFileSinkService.EngineLogFileName);
    }

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
    public string LogsDirectory { get; }
}
