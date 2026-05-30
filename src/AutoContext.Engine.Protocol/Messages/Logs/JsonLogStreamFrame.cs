namespace AutoContext.Engine.Protocol.Messages.Logs;

using System.Text.Json.Serialization;

/// <summary>
/// Discriminated-union envelope for one frame on the engine's
/// <c>logs</c> named pipe. Every NDJSON line the engine emits on
/// the <c>logs</c> pipe is one <see cref="JsonLogStreamFrame"/>: a
/// <see cref="JsonLogRecordFrame"/> carrying a <see cref="JsonLogRecord"/>
/// for normal records, or a <see cref="JsonLogEvictedFrame"/> as the
/// terminal frame the broadcaster sends to a slow subscriber
/// before disconnecting it.
/// </summary>
/// <remarks>
/// <para>
/// The discriminator is the <c>kind</c> JSON property; subscribers
/// branch on it to project each frame to the right shape. The
/// engine's on-disk <c>engine.log</c> NDJSON file is
/// <see cref="JsonLogRecord"/> directly (no wrapper) — the wrapper
/// only exists on the <c>logs</c>-pipe wire, where the eviction
/// terminator requires a discriminator the disk file never needs.
/// See <c>design § events &gt; backpressure</c> for the
/// slow-subscriber eviction protocol.
/// </para>
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(JsonLogRecordFrame), typeDiscriminator: "record")]
[JsonDerivedType(typeof(JsonLogEvictedFrame), typeDiscriminator: "evicted")]
public abstract record JsonLogStreamFrame;
