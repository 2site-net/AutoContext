namespace AutoContext.Engine.Protocol.Messages.Lifecycle;

/// <summary>
/// String constants for the lifecycle-scoped JSON-RPC method
/// names the engine surfaces on the <c>events</c> pipe. Kept in
/// the protocol assembly so both sides reference the same
/// identifiers without copy-paste drift, and grouped alongside
/// the lifecycle DTOs (<see cref="LifecycleEvent"/>,
/// <see cref="LifecycleEventKinds"/>) they pair with.
/// </summary>
public static class LifecycleMethods
{
    /// <summary>
    /// Conceptual method name a client invokes by connecting to
    /// the <c>events</c> pipe and completing <c>Engine.Hello</c>:
    /// the act of binding to that pipe IS the subscription. There
    /// is no actual RPC frame on the wire for this method —
    /// <c>events</c> only carries server-pushed notifications —
    /// but the name is reserved here for documentation and
    /// forward compatibility with future per-subscription
    /// parameters.
    /// </summary>
    public const string Subscribe = "Engine.Lifecycle.Subscribe";

    /// <summary>
    /// Method name carried on every server-pushed lifecycle
    /// <see cref="JsonRpc.JsonRpcNotification"/>. Acts as the
    /// broadcast-family discriminator on the <c>events</c> pipe,
    /// letting future broadcast families (e.g. agent events)
    /// share the same wire frame shape.
    /// </summary>
    public const string Notification = "Engine.Lifecycle";
}
