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
}
