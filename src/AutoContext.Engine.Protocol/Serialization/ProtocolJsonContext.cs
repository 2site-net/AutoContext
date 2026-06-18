namespace AutoContext.Engine.Protocol.Serialization;

using System.Text.Json;
using System.Text.Json.Serialization;

using AutoContext.Engine.Protocol;
using AutoContext.Engine.Protocol.JsonRpc;
using AutoContext.Engine.Protocol.Messages;
using AutoContext.Engine.Protocol.Messages.Config;
using AutoContext.Engine.Protocol.Messages.Instructions;
using AutoContext.Engine.Protocol.Messages.Lifecycle;
using AutoContext.Engine.Protocol.Messages.Logs;
using AutoContext.Engine.Protocol.Messages.McpTools;
using AutoContext.Engine.Protocol.Messages.Registry;
using AutoContext.Engine.Protocol.Messages.Workspace;

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
[JsonSerializable(typeof(JsonRpcNotification))]
[JsonSerializable(typeof(JsonRpcStreamFrame))]
[JsonSerializable(typeof(JsonHandshakeParams))]
[JsonSerializable(typeof(JsonHandshakeResult))]
[JsonSerializable(typeof(JsonLifecycleEvent))]
[JsonSerializable(typeof(JsonLogRecord))]
[JsonSerializable(typeof(JsonLogEventId))]
[JsonSerializable(typeof(JsonLogExceptionInfo))]
[JsonSerializable(typeof(JsonLogStreamFrame))]
[JsonSerializable(typeof(JsonLogRecordFrame))]
[JsonSerializable(typeof(JsonLogDroppedFrame))]
[JsonSerializable(typeof(JsonLogsGetEngineParams))]
[JsonSerializable(typeof(JsonLogsGetEngineResult))]
[JsonSerializable(typeof(JsonRegistryEntry))]
[JsonSerializable(typeof(JsonRegistryEntriesResult))]
[JsonSerializable(typeof(JsonConfigSnapshot))]
[JsonSerializable(typeof(JsonConfigStreamFrame))]
[JsonSerializable(typeof(JsonConfigSnapshotFrame))]
[JsonSerializable(typeof(JsonConfigDroppedFrame))]
[JsonSerializable(typeof(JsonConfigToggleFileParams))]
[JsonSerializable(typeof(JsonConfigToggleRuleParams))]
[JsonSerializable(typeof(JsonInstructionsListParams))]
[JsonSerializable(typeof(JsonInstructionsListResult))]
[JsonSerializable(typeof(JsonInstructionsListRow))]
[JsonSerializable(typeof(JsonInstructionsSection))]
[JsonSerializable(typeof(JsonInstructionsCategoriesResult))]
[JsonSerializable(typeof(JsonInstructionsCategory))]
[JsonSerializable(typeof(JsonInstructionsGetParams))]
[JsonSerializable(typeof(JsonInstructionsGetResult))]
[JsonSerializable(typeof(JsonInstructionsGetOkResult))]
[JsonSerializable(typeof(JsonInstructionsGetDisabledResult))]
[JsonSerializable(typeof(JsonInstructionsGetNotFoundResult))]
[JsonSerializable(typeof(JsonInstructionsFile))]
[JsonSerializable(typeof(JsonInstructionsFilesResult))]
[JsonSerializable(typeof(JsonInstructionsGetRawParams))]
[JsonSerializable(typeof(JsonInstructionsGetRawResult))]
[JsonSerializable(typeof(JsonInstructionsGetRawOkResult))]
[JsonSerializable(typeof(JsonInstructionsGetRawNotFoundResult))]
[JsonSerializable(typeof(JsonInstructionsSearchContentParams))]
[JsonSerializable(typeof(JsonInstructionsSearchContentResult))]
[JsonSerializable(typeof(JsonInstructionsContentHit))]
[JsonSerializable(typeof(JsonInstructionsContentExcerpt))]
[JsonSerializable(typeof(JsonInstructionsStreamFrame))]
[JsonSerializable(typeof(JsonInstructionsSnapshotFrame))]
[JsonSerializable(typeof(JsonInstructionsDroppedFrame))]
[JsonSerializable(typeof(JsonMcpToolsListResult))]
[JsonSerializable(typeof(JsonMcpToolsListRow))]
[JsonSerializable(typeof(JsonMcpToolsInvokeParams))]
[JsonSerializable(typeof(JsonMcpToolsInvokeResult))]
[JsonSerializable(typeof(JsonMcpToolsInvokeOkResult))]
[JsonSerializable(typeof(JsonMcpToolsInvokeToolErrorResult))]
[JsonSerializable(typeof(JsonMcpToolsInvokeSchemaErrorResult))]
[JsonSerializable(typeof(JsonMcpToolsInvokeDisabledResult))]
[JsonSerializable(typeof(JsonMcpToolsInvokeNotFoundResult))]
[JsonSerializable(typeof(JsonMcpToolsSchemaError))]
[JsonSerializable(typeof(JsonWorkspaceFlags))]
[JsonSerializable(typeof(JsonWorkspaceDetectResult))]
[JsonSerializable(typeof(JsonWorkspaceInfoResult))]
[JsonSerializable(typeof(JsonShutdownResult))]
public sealed partial class ProtocolJsonContext : JsonSerializerContext
{
}
