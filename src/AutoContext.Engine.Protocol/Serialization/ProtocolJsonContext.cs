namespace AutoContext.Engine.Protocol.Serialization;

using System.Text.Json;
using System.Text.Json.Serialization;

using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Messages.Registry;

/// <summary>
/// System.Text.Json source-generation context for every wire shape
/// in <c>AutoContext.Engine.Protocol</c>. Centralising the
/// <c>[JsonSerializable]</c> declarations on one partial class
/// guarantees AOT-safe codegen for the entire protocol surface
/// without scattering converter wiring across the codebase.
/// </summary>
/// <remarks>
/// Add a <c>[JsonSerializable(typeof(...))]</c> entry whenever a
/// new wire DTO is introduced under <c>JsonRpc/</c> or <c>Messages/</c>.
/// </remarks>
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(JsonRpcRequest))]
[JsonSerializable(typeof(JsonRpcResponse))]
[JsonSerializable(typeof(JsonRpcError))]
[JsonSerializable(typeof(HandshakeParams))]
[JsonSerializable(typeof(HandshakeResult))]
[JsonSerializable(typeof(RegistryEntry))]
[JsonSerializable(typeof(RegistryEntriesResult))]
[JsonSerializable(typeof(ShutdownResult))]
public sealed partial class ProtocolJsonContext : JsonSerializerContext
{
}
