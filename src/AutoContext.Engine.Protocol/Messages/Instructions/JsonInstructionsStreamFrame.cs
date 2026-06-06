namespace AutoContext.Engine.Protocol.Messages.Instructions;

using System.Text.Json.Serialization;

/// <summary>
/// Discriminated-union envelope for one frame on the engine's
/// <see cref="InstructionsMethods.Subscribe"/> RPC stream. Every frame
/// is one <see cref="JsonInstructionsStreamFrame"/>: a
/// <see cref="JsonInstructionsSnapshotFrame"/> carrying the current
/// listing, or a <see cref="JsonInstructionsDroppedFrame"/> as the
/// terminal frame the broadcaster sends to a slow subscriber before
/// disconnecting it.
/// </summary>
/// <remarks>
/// <para>
/// The discriminator is the <c>kind</c> JSON property. Like
/// <c>Config.Subscribe</c>, this is a keyed-state stream: every new
/// subscriber receives the current listing as the first frame
/// (snapshot-on-subscribe), so a late subscriber never needs a
/// separate <see cref="InstructionsMethods.List"/>. Subsequent frames
/// arrive on every corpus reload. See <c>design § events &gt;
/// backpressure</c> for the slow-subscriber drop protocol.
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(JsonInstructionsSnapshotFrame), typeDiscriminator: "snapshot")]
[JsonDerivedType(typeof(JsonInstructionsDroppedFrame), typeDiscriminator: "dropped")]
public abstract record JsonInstructionsStreamFrame;
