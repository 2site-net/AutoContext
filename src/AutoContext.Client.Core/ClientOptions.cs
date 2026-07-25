namespace AutoContext.Client.Core;

/// <summary>
/// Composition-time configuration for an <c>AutoContext.Client.Core</c>
/// registration. Surfaces the launcher identity the client dials
/// under, the spawn policy that governs whether a missing engine is
/// started, and the engine-spawn switches the resolver forwards when
/// it cold-spawns.
/// </summary>
/// <remarks>
/// Property values are checked by <see cref="ClientOptionsValidator"/>
/// when the host's options pipeline first materialises an
/// <see cref="ClientOptions"/> instance, so an invalid value surfaces
/// as a host startup failure rather than a deferred dial-time crash.
/// </remarks>
public sealed class ClientOptions
{
    /// <summary>
    /// Maximum length of <see cref="InstanceLabel"/>, in characters.
    /// Matches the engine's printable-ASCII length cap.
    /// </summary>
    public const int InstanceLabelMaxLength = 200;

    /// <summary>
    /// Absolute path of the workspace this client resolves an engine
    /// for. Hashed to the <c>&lt;workspaceHash&gt;</c> endpoint segment
    /// using the same normalisation the engine applies, so a
    /// one-character drift talks to a different engine. Mandatory.
    /// </summary>
    public string WorkspacePath { get; set; } = string.Empty;

    /// <summary>
    /// UUIDv4 identifying this launcher instance. Becomes the
    /// <c>&lt;instanceId&gt;</c> endpoint segment and is passed verbatim
    /// to any engine this client cold-spawns. Defaults to a fresh
    /// UUIDv4 minted per <see cref="ClientOptions"/> instance.
    /// </summary>
    public Guid InstanceId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Freeform human-readable descriptor forwarded on
    /// <c>--instance-label</c> when the resolver cold-spawns an engine.
    /// Pure observability; has no effect on dial behaviour. Defaults to
    /// the empty string, which the resolver omits from the spawn argv.
    /// </summary>
    public string InstanceLabel { get; set; } = string.Empty;

    /// <summary>
    /// When <see langword="true"/> the resolver connects to an existing
    /// engine or fails; it never spawns one. Used by tests and by
    /// observe-only callers that must not start an engine as a
    /// side effect.
    /// </summary>
    public bool SpawnDisabled { get; set; }

    /// <summary>
    /// Absolute path of the <c>autocontext-engine</c> binary the
    /// resolver launches on cold start. When <see langword="null"/> the
    /// resolver locates the binary at the nested side-car path under
    /// <see cref="AppContext.BaseDirectory"/>.
    /// </summary>
    public string? EngineBinaryPath { get; set; }

    /// <summary>
    /// Idle-timeout forwarded on <c>--idle-timeout</c> when the resolver
    /// cold-spawns an engine. <see cref="TimeSpan.Zero"/> disables the
    /// engine's idle gate — the mode long-lived embedders want so the
    /// engine outlives a single dial. When <see langword="null"/> the
    /// switch is omitted and the spawned engine keeps its own default.
    /// </summary>
    public TimeSpan? IdleTimeout { get; set; }
}
