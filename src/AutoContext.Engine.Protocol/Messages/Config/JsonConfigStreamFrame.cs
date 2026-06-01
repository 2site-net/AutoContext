namespace AutoContext.Engine.Protocol.Messages.Config;

using System.Text.Json.Serialization;

/// <summary>
/// Discriminated-union envelope for one frame on the engine's
/// <c>Config.Subscribe</c> RPC stream. Every frame the engine emits
/// is one <see cref="JsonConfigStreamFrame"/>: a
/// <see cref="JsonConfigSnapshotFrame"/> carrying the full
/// <see cref="JsonConfigSnapshot"/> for the current state, or a
/// <see cref="JsonConfigEvictedFrame"/> as the terminal frame the
/// broadcaster sends to a slow subscriber before disconnecting it.
/// </summary>
/// <remarks>
/// <para>
/// The discriminator is the <c>kind</c> JSON property; subscribers
/// branch on it to project each frame to the right shape. Unlike a
/// pure live tail, <c>Config.Subscribe</c> is a keyed-state stream:
/// every new subscriber receives the current snapshot as the first
/// frame (snapshot-on-subscribe), so a late subscriber never needs a
/// separate <c>Config.Get</c>. See <c>design § events &gt;
/// backpressure</c> for the slow-subscriber eviction protocol.
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(JsonConfigSnapshotFrame), typeDiscriminator: "snapshot")]
[JsonDerivedType(typeof(JsonConfigEvictedFrame), typeDiscriminator: "evicted")]
public abstract record JsonConfigStreamFrame;
