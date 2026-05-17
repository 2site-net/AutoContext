namespace AutoContext.Engine.Protocol.Messages;

/// <summary>
/// String constants for the engine's JSON-RPC method names. Kept
/// in the protocol assembly so both sides of the wire reference
/// the same identifiers without copy-paste drift.
/// </summary>
public static class ProtocolMethods
{
    /// <summary>
    /// Mandatory first frame on every <c>rpc</c> and <c>events</c>
    /// pipe per <c>design § Lifecycle &gt; Wire-protocol handshake</c>.
    /// </summary>
    public const string Hello = "Engine.Hello";
}
