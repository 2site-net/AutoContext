namespace AutoContext.Engine.Protocol.Messages.Config;

/// <summary>
/// JSON-RPC method-name constants for the <c>Config.*</c> family —
/// the engine's authority over the workspace's
/// <c>.autocontext.json</c> state. Grouped here so handlers and
/// transports share one spelling of each dotted method name per
/// <c>design § RPC surface</c>.
/// </summary>
public static class ConfigMethods
{
    /// <summary>
    /// Reads the current config snapshot the engine holds in memory.
    /// Takes no params; returns the full
    /// <see cref="JsonConfigSnapshot"/>.
    /// </summary>
    public const string Get = "Config.Get";

    /// <summary>
    /// Flips the whole-file disabled state of one instruction file.
    /// Takes <see cref="JsonConfigToggleFileParams"/>; returns the
    /// resulting <see cref="JsonConfigSnapshot"/>.
    /// </summary>
    public const string ToggleFile = "Config.ToggleFile";

    /// <summary>
    /// Flips the disabled state of one rule within an instruction
    /// file. Takes <see cref="JsonConfigToggleRuleParams"/>; returns
    /// the resulting <see cref="JsonConfigSnapshot"/>.
    /// </summary>
    public const string ToggleRule = "Config.ToggleRule";

    /// <summary>
    /// Opens a server-streaming subscription to the engine's config
    /// state. Takes no params; the engine emits one
    /// <see cref="JsonConfigStreamFrame"/> per frame — a
    /// <see cref="JsonConfigSnapshotFrame"/> with the current
    /// snapshot at subscribe time (snapshot-on-subscribe) and again
    /// on every subsequent change, or a terminal
    /// <see cref="JsonConfigDroppedFrame"/> for a slow subscriber.
    /// A late subscriber never needs a separate <see cref="Get"/>.
    /// </summary>
    public const string Subscribe = "Config.Subscribe";
}
