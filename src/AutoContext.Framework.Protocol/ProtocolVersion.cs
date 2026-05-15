namespace AutoContext.Framework.Protocol;

/// <summary>
/// Wire-protocol version constant carried in every <c>Engine.Hello</c>
/// handshake. Engine and client must agree on the integer; mismatch in
/// either direction refuses — there is no negotiation and no fallback.
/// </summary>
/// <remarks>
/// Bumped on every wire-format change (any non-additive change to a DTO
/// under <c>Messages/</c> or an envelope shape under <c>Envelopes/</c>).
/// Each host bundles its own engine binary, so a production handshake
/// mismatch is a packaging bug, not a scenario the protocol tries to
/// recover from — see <c>design § Lifecycle &gt; Wire-protocol handshake</c>.
/// </remarks>
public static class ProtocolVersion
{
    /// <summary>
    /// Current wire-protocol version. Exact-match comparison only.
    /// </summary>
    public const int Current = 1;
}
