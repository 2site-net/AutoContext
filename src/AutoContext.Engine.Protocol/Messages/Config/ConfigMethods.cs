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
}
