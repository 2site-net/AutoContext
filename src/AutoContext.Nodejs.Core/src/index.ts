// AutoContext.Nodejs.Core — shared TypeScript infrastructure for VS
// Code and Node-based AutoContext components. Re-exports the public
// surface of the package so consumers can import directly from
// `autocontext-nodejs-core`.

export type { ChannelLogger } from './types/channel-logger.js';
export { LogCategory } from './logging/log-category.js';
export { LogLevel } from './logging/log-level.js';
export type { Logger } from './types/logger.js';
export { LoggerBase } from './logging/logger-base.js';
export type { LoggerFacade } from './types/logger-facade.js';
export { NullLogger } from './logging/null-logger.js';
export { EndpointKind, createInstanceId, formatEndpoint } from './engine/endpoint.js';
export {
    DEFAULT_ENGINE_CONNECT_BUDGET,
    nextRetryDelayMs,
} from './engine/engine-connect-budget.js';
export type { EngineConnectBudget } from './engine/engine-connect-budget.js';
export { EngineConnection } from './engine/engine-connection.js';
export { EngineConnector } from './engine/engine-connector.js';
export type { EngineConnectorOptions } from './engine/engine-connector.js';
export { EngineDaemonManager } from './engine/engine-daemon-manager.js';
export type { EngineDaemonManagerOptions } from './engine/engine-daemon-manager.js';
export {
    EngineProtocolError,
    EngineRpcError,
    EngineSubscriptionDroppedError,
    EngineUnavailableError,
} from './engine/engine-errors.js';
export { resolveEngineBinaryPath } from './engine/engine-locator.js';
export type { EngineBinaryLocation } from './engine/engine-locator.js';
export { EngineMethods } from './engine/engine-methods.js';
export { EngineSpawner, buildEngineArgv } from './engine/engine-spawner.js';
export type { EngineSpawnRequest } from './engine/engine-spawner.js';
export { EngineSubscriptionChannel } from './engine/engine-subscription-channel.js';
export { PROTOCOL_VERSION } from './engine/json-rpc.js';
export { WORKSPACE_HASH_LENGTH, computeWorkspaceHash } from './engine/workspace-hash.js';
export type {
    JsonAgentCompactedParams,
    JsonAgentEvent,
    JsonAgentSubagentStartedParams,
    JsonAgentSubagentStoppedParams,
    JsonAgentToolUsedParams,
    JsonAgentTurnEndedParams,
} from './types/json-agent-messages.js';
export type {
    JsonConfigDiagnostic,
    JsonConfigInstructionsFile,
    JsonConfigInstructionsRule,
    JsonConfigMcpTool,
    JsonConfigSnapshot,
    JsonConfigStreamFrame,
    JsonConfigToggleFileParams,
    JsonConfigToggleRuleParams,
} from './types/json-config-messages.js';
export type {
    JsonDiscoveryRouteForPromptParams,
    JsonDiscoveryRouteForPromptResult,
    JsonDiscoveryRouteForToolParams,
    JsonDiscoveryRouteForToolResult,
} from './types/json-discovery-messages.js';
export type {
    JsonHandshakeResult,
    JsonShutdownResult,
} from './types/json-engine-messages.js';
export type {
    InstructionsRawSource,
    InstructionsSource,
    JsonInstructionsCategoriesResult,
    JsonInstructionsCategory,
    JsonInstructionsContentExcerpt,
    JsonInstructionsContentHit,
    JsonInstructionsFile,
    JsonInstructionsFilesResult,
    JsonInstructionsGetParams,
    JsonInstructionsGetRawParams,
    JsonInstructionsGetRawResult,
    JsonInstructionsGetResult,
    JsonInstructionsListParams,
    JsonInstructionsListResult,
    JsonInstructionsListRow,
    JsonInstructionsMetadataFieldInfo,
    JsonInstructionsMetadataMatch,
    JsonInstructionsSearchByMetadataParams,
    JsonInstructionsSearchByMetadataResult,
    JsonInstructionsSearchContentParams,
    JsonInstructionsSearchContentResult,
    JsonInstructionsSection,
    JsonInstructionsStreamFrame,
} from './types/json-instructions-messages.js';
export type { JsonLifecycleEvent } from './types/json-lifecycle-messages.js';
export type {
    JsonLogEventId,
    JsonLogExceptionInfo,
    JsonLogRecord,
    JsonLogStreamFrame,
    JsonLogsGetEngineParams,
    JsonLogsGetEngineResult,
    JsonLogsGetWorkerParams,
    JsonLogsGetWorkerResult,
    JsonLogsTailWorkerParams,
} from './types/json-logs-messages.js';
export type {
    JsonMcpToolsInvokeParams,
    JsonMcpToolsInvokeResult,
    JsonMcpToolsListResult,
    JsonMcpToolsListRow,
    JsonMcpToolsSchemaError,
} from './types/json-mcp-tools-messages.js';
export type {
    JsonRegistryEntriesResult,
    JsonRegistryEntry,
} from './types/json-registry-messages.js';
export type {
    JsonWorkspaceDetectResult,
    JsonWorkspaceFlags,
    JsonWorkspaceInfoResult,
} from './types/json-workspace-messages.js';
export { LengthPrefixedFrameCodec } from './pipes/length-prefixed-frame-codec.js';
export { PipeEventsSubscriptionClient } from './pipes/pipe-events-subscription-client.js';
export type {
    PipeEventsSubscriptionClientOptions,
    PipeFrameExchange,
} from './pipes/pipe-events-subscription-client.js';
export { BoundPipeListener, PipeListener } from './pipes/pipe-listener.js';
export { PipeKeepAliveClient } from './pipes/pipe-keep-alive-client.js';
export { PipeRpcExchangeClient } from './pipes/pipe-rpc-exchange-client.js';
export type { PipeRpcExchangeClientOptions } from './pipes/pipe-rpc-exchange-client.js';
export { PipeStreamingClient } from './pipes/pipe-streaming-client.js';
export type { PipeStreamingClientOptions } from './pipes/pipe-streaming-client.js';
export { PipeTransport } from './pipes/pipe-transport.js';
