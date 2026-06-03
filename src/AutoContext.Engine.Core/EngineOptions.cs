namespace AutoContext.Engine.Core;

using AutoContext.Engine.Core.Infrastructure;
using AutoContext.Engine.Core.Logging;

/// <summary>
/// Composition-time configuration for an
/// <c>AutoContext.Engine.Core</c> registration. Surfaces both the
/// <c>--*</c> CLI knobs from
/// <c>design § Engine options (CLI surface)</c> and the
/// library-only knobs that deliberately do not appear on the
/// engine binary's argv (see
/// <c>design § Engine options &gt; Library-only knobs</c>).
/// </summary>
/// <remarks>
/// <para>
/// Property values are checked by <see cref="EngineOptionsValidator"/>
/// when the host's options pipeline first materialises an
/// <see cref="EngineOptions"/> instance, so invalid values surface as
/// a host startup failure rather than a deferred runtime crash.
/// The validator enforces only the shape rules called out in the
/// design (path rootedness, label charset, idle-timeout sign, etc.);
/// semantic gating (workspace-existence on disk, parent-pid
/// liveness) is the responsibility of the hosted services that read
/// these values.
/// </para>
/// <para>
/// Defaults match the <i>daemon</i> role of the engine binary.
/// Callers that compose the engine in-process (tests, embedders)
/// are still expected to set <see cref="WorkspacePath"/> and
/// <see cref="InstanceId"/> explicitly; both have no meaningful
/// default.
/// </para>
/// </remarks>
public sealed class EngineOptions : IWorkspaceEngineInfo
{
    /// <summary>
    /// Maximum length of <see cref="InstanceLabel"/>, in characters.
    /// Matches the printable-ASCII length cap in
    /// <c>design § Engine options &gt; --instance-label</c>.
    /// </summary>
    public const int InstanceLabelMaxLength = 200;

    /// <summary>
    /// Default idle-timeout per the <c>--idle-timeout</c> switch.
    /// 300 seconds matches the daemon-role default in
    /// <c>design § Engine options</c>.
    /// </summary>
    public static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromSeconds(300);

    /// <summary>
    /// Default retention window per the <c>--retention</c> switch.
    /// One day matches the daemon-role default in
    /// <c>design § Engine options</c>.
    /// </summary>
    public static readonly TimeSpan DefaultRetention = TimeSpan.FromDays(1);

    /// <summary>
    /// Default upper bound on how long <c>LifecycleService.StopAsync</c>
    /// waits for connected <c>events</c>-pipe subscribers to drain
    /// the terminal <c>shutting-down</c> frame off the wire before
    /// the service tears the connection down regardless. Five
    /// seconds is generous for well-behaved peers and short enough
    /// to keep host shutdown bounded when a peer is stuck or
    /// already dead.
    /// </summary>
    public static readonly TimeSpan DefaultShutdownDrainTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Override for the engine cache root. When
    /// <see langword="null"/> the engine resolves the cache root
    /// the usual way (Windows: <c>%LOCALAPPDATA%/autocontext</c>;
    /// POSIX: <c>$XDG_CACHE_HOME/autocontext</c> or
    /// <c>$HOME/.cache/autocontext</c>); tests and embedders that
    /// need to isolate the registry, log subtrees, and housekeeping
    /// targets set this to an absolute path. Surfaced on the
    /// engine binary as <c>--cache-root &lt;absolute-path&gt;</c>
    /// in both the daemon and <c>--mcp-server with-stdio</c> roles.
    /// </summary>
    public string? CacheRootOverride { get; set; }

    /// <summary>
    /// Library-only override for the instructions-corpus root. When
    /// <see langword="null"/> the engine resolves the corpus the
    /// usual way; tests and embedders that need to point the engine
    /// at a temporary corpus tree set this to an absolute path.
    /// Deliberately not surfaced on the CLI.
    /// </summary>
    public string? CorpusRootOverride { get; set; }

    /// <summary>
    /// Idle-shutdown window. <see cref="TimeSpan.Zero"/> is the
    /// explicit "disable the idle gate" sentinel — the engine then
    /// lives until killed by signal, by
    /// <c>Engine.Shutdown</c>, or by the
    /// <see cref="ParentProcessId"/> watchdog. Defaults to
    /// <see cref="DefaultIdleTimeout"/>.
    /// </summary>
    public TimeSpan IdleTimeout { get; set; } = DefaultIdleTimeout;

    /// <summary>
    /// UUIDv4 the launcher minted once per launcher instance.
    /// Mandatory; becomes the <c>&lt;instanceId&gt;</c> segment of
    /// every endpoint per
    /// <c>design § Lifecycle &gt; Endpoint</c>. The engine validates
    /// only the GUID shape; it does not interpret the bytes further.
    /// </summary>
    public Guid InstanceId { get; set; }

    /// <summary>
    /// Optional freeform human-readable descriptor the launcher
    /// attached to this engine instance. Defaults to the empty
    /// string. Has no semantic effect on engine behaviour — it is
    /// pure observability. See
    /// <c>design § Engine options &gt; --instance-label</c> for the
    /// printable-ASCII charset rule and length cap.
    /// </summary>
    public string InstanceLabel { get; set; } = string.Empty;

    /// <summary>
    /// Log-rotation verbosity per the <c>--logging</c> switch.
    /// Defaults to <see cref="LogVerbosity.Normal"/>.
    /// </summary>
    public LogVerbosity Logging { get; set; } = LogVerbosity.Normal;

    /// <summary>
    /// MCP-server capability mode. Defaults to
    /// <see cref="EngineMcpServerMode.Off"/>.
    /// </summary>
    public EngineMcpServerMode McpServerMode { get; set; } = EngineMcpServerMode.Off;

    /// <summary>
    /// Optional parent-process watchdog. When set, the engine
    /// watches the named OS process via <c>Process.StartTime</c> +
    /// <c>WaitForExitAsync</c> and self-exits when that process
    /// vanishes. See
    /// <c>design § Engine options &gt; --parent-pid</c>.
    /// </summary>
    public int? ParentProcessId { get; set; }

    /// <summary>
    /// Housekeeping retention window. <see cref="TimeSpan.Zero"/>
    /// disables retention entirely (sweep deletes immediately on
    /// shutdown). Defaults to <see cref="DefaultRetention"/>.
    /// </summary>
    public TimeSpan Retention { get; set; } = DefaultRetention;

    /// <summary>
    /// Upper bound on how long <c>LifecycleService.StopAsync</c>
    /// waits for connected <c>events</c>-pipe subscribers to read
    /// the terminal <c>shutting-down</c> frame off the wire before
    /// the service forcibly tears the connection down. Bounds
    /// host-shutdown latency in the face of stuck or vanished
    /// peers; a value of <see cref="TimeSpan.Zero"/> means "do not
    /// wait for the drain at all" (peers that aren't already
    /// reading will miss the frame). Defaults to
    /// <see cref="DefaultShutdownDrainTimeout"/>.
    /// </summary>
    public TimeSpan ShutdownDrainTimeout { get; set; } = DefaultShutdownDrainTimeout;

    /// <summary>
    /// Absolute filesystem path of the workspace this engine pins
    /// to. Mandatory; there is no auto-detection. The workspace
    /// identity is the path, not the launcher's CWD.
    /// </summary>
    public string WorkspacePath { get; set; } = string.Empty;
}
