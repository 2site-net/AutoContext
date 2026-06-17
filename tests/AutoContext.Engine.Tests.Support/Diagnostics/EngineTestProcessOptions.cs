namespace AutoContext.Engine.Tests.Support.Diagnostics;

/// <summary>
/// CLI-shaped configuration for a single spawned
/// <c>autocontext-engine</c> process. Each property maps 1:1 onto a
/// <c>--*</c> switch the engine binary accepts, so the harness can
/// never express something the real argv surface cannot — the
/// inverse-parser drift and silent-drop hazards of reusing the
/// in-process <c>EngineOptions</c> composition type are avoided by
/// construction.
/// </summary>
/// <remarks>
/// <para>
/// Defaults are tuned for the integration suite, not the daemon
/// role: <see cref="IdleTimeout"/> is <see cref="TimeSpan.Zero"/> so
/// the idle gate cannot race a test budget, and
/// <see cref="ParentProcessId"/> is the current test process so a
/// crashed run cannot leak a stale engine.
/// </para>
/// <para>
/// <see cref="WorkspacePath"/> and <see cref="InstanceId"/> are
/// optional: a solo-engine test may leave them unset and let
/// <see cref="EngineTestProcess.SpawnAsync"/> mint a fresh isolated
/// workspace and instance id. Multi-engine tests that need two
/// engines to share a workspace and cache root must set
/// <see cref="WorkspacePath"/> (and <see cref="CacheRootOverride"/>)
/// explicitly on both.
/// </para>
/// </remarks>
public sealed class EngineTestProcessOptions
{
    /// <summary>
    /// Absolute workspace path passed via <c>--workspace</c>. When
    /// <see langword="null"/>, <see cref="EngineTestProcess.SpawnAsync"/>
    /// allocates a fresh isolated workspace directory.
    /// </summary>
    public string? WorkspacePath { get; set; }

    /// <summary>
    /// Instance id passed via <c>--instance-id</c>. When
    /// <see langword="null"/>, <see cref="EngineTestProcess.SpawnAsync"/>
    /// mints a fresh <see cref="Guid"/>.
    /// </summary>
    public Guid? InstanceId { get; set; }

    /// <summary>
    /// Cache-root override passed via <c>--cache-root</c>. When
    /// <see langword="null"/>, the engine resolves its cache root the
    /// usual way. Two engines sharing a value here share a cache
    /// root — exactly what the cross-engine housekeeping tests need.
    /// </summary>
    public string? CacheRootOverride { get; set; }

    /// <summary>
    /// Resources-root (side-car) override passed via
    /// <c>--resources-root</c>. When <see langword="null"/>, the engine
    /// resolves its resources root the usual way
    /// (<c>AppContext.BaseDirectory/Resources</c>). Integration tests
    /// that need the spawned engine to read a substitute side-car tree
    /// — for example a <c>workers.json</c> + <c>mcp-tools-registry.json</c>
    /// pair that dispatches to a stand-in worker — point this at an
    /// absolute path to that tree.
    /// </summary>
    public string? ResourcesRootOverride { get; set; }

    /// <summary>
    /// Idle-shutdown window passed via <c>--idle-timeout</c> as whole
    /// seconds. Defaults to <see cref="TimeSpan.Zero"/> (the
    /// "disable the idle gate" sentinel) so the gate cannot race the
    /// test budget.
    /// </summary>
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Parent-process watchdog target passed via <c>--parent-pid</c>.
    /// Defaults to the current test process so a crashed run cannot
    /// leak a stale engine. Set to <see langword="null"/> to omit the
    /// switch entirely.
    /// </summary>
    public int? ParentProcessId { get; set; } = System.Environment.ProcessId;

    /// <summary>
    /// Retention window passed verbatim via <c>--retention</c>, using
    /// the engine's grammar (<c>'0'</c> or <c>&lt;n&gt;{s|m|h|d}</c>).
    /// When <see langword="null"/>, the switch is omitted and the
    /// engine applies its default retention window.
    /// </summary>
    public string? Retention { get; set; }

    /// <summary>
    /// Additional raw CLI arguments appended after the first-classed
    /// switches above. An escape hatch for knobs the suite has not
    /// promoted to a dedicated property yet.
    /// </summary>
    public IReadOnlyList<string> ExtraArguments { get; set; } = [];
}
