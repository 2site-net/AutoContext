namespace AutoContext.Engine.Protocol.JsonRpc;

/// <summary>
/// Constant for the JSON-RPC 2.0 protocol marker carried in every
/// request and response envelope.
/// </summary>
public static class JsonRpcVersion
{
    /// <summary>
    /// The literal <c>"2.0"</c> value required on the
    /// <c>jsonrpc</c> field of every frame.
    /// </summary>
    public const string Value = "2.0";
}
