import type { LoggerFacade } from '#types/logger-facade.js';
import { NullLogger } from '../logging/null-logger.js';
import { PipeTransport } from '../pipes/pipe-transport.js';
import { createInstanceId } from './endpoint.js';
import type { EngineConnectBudget } from './engine-connect-budget.js';
import type { EngineConnection } from './engine-connection.js';
import { EngineConnector } from './engine-connector.js';
import { EngineSubscriptionDroppedError } from './engine-errors.js';
import type { EngineSubscriptionChannel } from './engine-subscription-channel.js';
import { resolveEngineBinaryPath, type EngineBinaryLocation } from './engine-locator.js';
import { EngineMethods } from './engine-methods.js';
import { EngineSpawner } from './engine-spawner.js';
import { computeWorkspaceHash } from './workspace-hash.js';
import type {
    JsonAgentCompactedParams,
    JsonAgentEvent,
    JsonAgentSubagentStartedParams,
    JsonAgentSubagentStoppedParams,
    JsonAgentToolUsedParams,
    JsonAgentTurnEndedParams,
} from '#types/json-agent-messages.js';
import type {
    JsonConfigSnapshot,
    JsonConfigStreamFrame,
    JsonConfigToggleFileParams,
    JsonConfigToggleRuleParams,
} from '#types/json-config-messages.js';
import type {
    JsonDiscoveryRouteForPromptParams,
    JsonDiscoveryRouteForPromptResult,
    JsonDiscoveryRouteForToolParams,
    JsonDiscoveryRouteForToolResult,
} from '#types/json-discovery-messages.js';
import type { JsonShutdownResult } from '#types/json-engine-messages.js';
import type {
    InstructionsRawSource,
    JsonInstructionsCategoriesResult,
    JsonInstructionsFilesResult,
    JsonInstructionsGetParams,
    JsonInstructionsGetRawParams,
    JsonInstructionsGetRawResult,
    JsonInstructionsGetResult,
    JsonInstructionsListParams,
    JsonInstructionsListResult,
    JsonInstructionsListRow,
    JsonInstructionsSearchByMetadataParams,
    JsonInstructionsSearchByMetadataResult,
    JsonInstructionsSearchContentParams,
    JsonInstructionsSearchContentResult,
    JsonInstructionsStreamFrame,
} from '#types/json-instructions-messages.js';
import type { JsonLifecycleEvent } from '#types/json-lifecycle-messages.js';
import type {
    JsonLogRecord,
    JsonLogStreamFrame,
    JsonLogsGetEngineParams,
    JsonLogsGetEngineResult,
    JsonLogsGetWorkerParams,
    JsonLogsGetWorkerResult,
    JsonLogsTailWorkerParams,
} from '#types/json-logs-messages.js';
import type {
    JsonMcpToolsInvokeParams,
    JsonMcpToolsInvokeResult,
    JsonMcpToolsListResult,
} from '#types/json-mcp-tools-messages.js';
import type { JsonRegistryEntriesResult } from '#types/json-registry-messages.js';
import type {
    JsonWorkspaceDetectResult,
    JsonWorkspaceInfoResult,
} from '#types/json-workspace-messages.js';

/** Construction options for {@link EngineDaemonManager}. */
export type EngineDaemonManagerOptions = EngineBinaryLocation & {
    /** Absolute path of the workspace this manager serves. */
    readonly workspacePath: string;

    /** Per-launch UUID. Defaults to a fresh one. */
    readonly instanceId?: string;

    /** Descriptor forwarded on `--instance-label` when spawning. */
    readonly instanceLabel?: string;

    /** Whole seconds forwarded on `--idle-timeout` when spawning. */
    readonly idleTimeoutSeconds?: number;

    /** When true the manager dials only and never spawns an engine. */
    readonly spawnDisabled?: boolean;

    /** Sink for lifecycle and connect diagnostics. */
    readonly logger?: LoggerFacade;

    /** Connect timing override. */
    readonly budget?: EngineConnectBudget;
};

/**
 * Owns the engine daemon on the Node side: resolves a live engine for
 * the workspace (dialing an existing one or spawning the bundled
 * binary), and exposes the engine's RPC surface as typed methods over
 * that connection.
 *
 * Unary calls share one held connection, which is opened on first use.
 * Each subscription takes a dedicated connection, because a stream
 * monopolises its connection's read side.
 *
 * Counterpart of the .NET `AutoContext.Client.Core` dialer.
 */
export class EngineDaemonManager {
    private readonly connector: EngineConnector;
    private readonly instanceIdValue: string;
    private readonly workspaceHashValue: string;

    private connection: EngineConnection | undefined;
    private connecting: Promise<EngineConnection> | undefined;
    private disposed = false;

    constructor(options: EngineDaemonManagerOptions) {
        if (options.workspacePath === '') {
            throw new Error('workspacePath must not be empty.');
        }

        const logger = options.logger ?? NullLogger.instance;

        this.instanceIdValue = options.instanceId ?? createInstanceId();
        this.workspaceHashValue = computeWorkspaceHash(options.workspacePath);

        this.connector = new EngineConnector({
            transport: new PipeTransport(logger),
            spawner: new EngineSpawner(logger),
            logger,
            workspacePath: options.workspacePath,
            workspaceHash: this.workspaceHashValue,
            instanceId: this.instanceIdValue,
            engineBinaryPath: resolveEngineBinaryPath(options),
            instanceLabel: options.instanceLabel,
            idleTimeoutSeconds: options.idleTimeoutSeconds,
            spawnDisabled: options.spawnDisabled,
            budget: options.budget,
        });
    }

    /** Per-launch UUID this manager's engine is addressed by. */
    get instanceId(): string {
        return this.instanceIdValue;
    }

    /** 16-uppercase-hex hash of the workspace path. */
    get workspaceHash(): string {
        return this.workspaceHashValue;
    }

    /** Endpoint address of the engine's `rpc` pipe. */
    get rpcAddress(): string {
        return this.connector.rpcAddress;
    }

    /**
     * Resolves a live engine, spawning one when none is listening.
     * Called implicitly by every RPC method.
     *
     * Concurrent first callers share one find-or-spawn round, so it is
     * bounded by the connect budget rather than by any one caller's
     * cancellation.
     */
    async start(): Promise<void> {
        await this.ensureConnected();
    }

    // -- Engine --------------------------------------------------------

    async registryEntries(signal?: AbortSignal): Promise<JsonRegistryEntriesResult> {
        return this.invoke(EngineMethods.RegistryEntries, undefined, signal);
    }

    async shutdown(signal?: AbortSignal): Promise<JsonShutdownResult> {
        return this.invoke(EngineMethods.Shutdown, undefined, signal);
    }

    // -- Config --------------------------------------------------------

    async configGet(signal?: AbortSignal): Promise<JsonConfigSnapshot> {
        return this.invoke(EngineMethods.ConfigGet, undefined, signal);
    }

    async configToggleFile(name: string, signal?: AbortSignal): Promise<JsonConfigSnapshot> {
        const params: JsonConfigToggleFileParams = { name };
        return this.invoke(EngineMethods.ConfigToggleFile, params, signal);
    }

    async configToggleRule(
        name: string,
        ruleId: string,
        signal?: AbortSignal,
    ): Promise<JsonConfigSnapshot> {
        const params: JsonConfigToggleRuleParams = { name, ruleId };
        return this.invoke(EngineMethods.ConfigToggleRule, params, signal);
    }

    // -- Instructions --------------------------------------------------

    async instructionsList(
        params?: JsonInstructionsListParams,
        signal?: AbortSignal,
    ): Promise<JsonInstructionsListResult> {
        return this.invoke(EngineMethods.InstructionsList, params, signal);
    }

    async instructionsCategories(signal?: AbortSignal): Promise<JsonInstructionsCategoriesResult> {
        return this.invoke(EngineMethods.InstructionsCategories, undefined, signal);
    }

    async instructionsGet(
        name: string,
        sections?: readonly string[],
        signal?: AbortSignal,
    ): Promise<JsonInstructionsGetResult> {
        const params: JsonInstructionsGetParams = { name, sections };
        return this.invoke(EngineMethods.InstructionsGet, params, signal);
    }

    async instructionsGetAll(signal?: AbortSignal): Promise<JsonInstructionsFilesResult> {
        return this.invoke(EngineMethods.InstructionsGetAll, undefined, signal);
    }

    async instructionsGetAlwaysAttached(
        signal?: AbortSignal,
    ): Promise<JsonInstructionsFilesResult> {
        return this.invoke(EngineMethods.InstructionsGetAlwaysAttached, undefined, signal);
    }

    async instructionsGetRaw(
        name: string,
        source: InstructionsRawSource,
        signal?: AbortSignal,
    ): Promise<JsonInstructionsGetRawResult> {
        const params: JsonInstructionsGetRawParams = { name, source };
        return this.invoke(EngineMethods.InstructionsGetRaw, params, signal);
    }

    async instructionsSearchContent(
        query: string,
        options?: { readonly limit?: number; readonly includeDisabled?: boolean },
        signal?: AbortSignal,
    ): Promise<JsonInstructionsSearchContentResult> {
        const params: JsonInstructionsSearchContentParams = {
            query,
            limit: options?.limit,
            includeDisabled: options?.includeDisabled,
        };
        return this.invoke(EngineMethods.InstructionsSearchContent, params, signal);
    }

    async instructionsSearchByMetadata(
        predicate: unknown,
        options?: { readonly includeSections?: boolean },
        signal?: AbortSignal,
    ): Promise<JsonInstructionsSearchByMetadataResult> {
        const params: JsonInstructionsSearchByMetadataParams = {
            predicate,
            includeSections: options?.includeSections,
        };
        return this.invoke(EngineMethods.InstructionsSearchByMetadata, params, signal);
    }

    // -- Workspace -----------------------------------------------------

    async workspaceDetect(signal?: AbortSignal): Promise<JsonWorkspaceDetectResult> {
        return this.invoke(EngineMethods.WorkspaceDetect, undefined, signal);
    }

    async workspaceInfo(signal?: AbortSignal): Promise<JsonWorkspaceInfoResult> {
        return this.invoke(EngineMethods.WorkspaceInfo, undefined, signal);
    }

    // -- MCP tools -----------------------------------------------------

    async mcpToolsList(signal?: AbortSignal): Promise<JsonMcpToolsListResult> {
        return this.invoke(EngineMethods.McpToolsList, undefined, signal);
    }

    async mcpToolsInvoke(
        name: string,
        toolArguments?: unknown,
        signal?: AbortSignal,
    ): Promise<JsonMcpToolsInvokeResult> {
        const params: JsonMcpToolsInvokeParams = { name, arguments: toolArguments };
        return this.invoke(EngineMethods.McpToolsInvoke, params, signal);
    }

    // -- Discovery -----------------------------------------------------

    async discoveryRouteForPrompt(
        prompt: string,
        signal?: AbortSignal,
    ): Promise<JsonDiscoveryRouteForPromptResult> {
        const params: JsonDiscoveryRouteForPromptParams = { prompt };
        return this.invoke(EngineMethods.DiscoveryRouteForPrompt, params, signal);
    }

    async discoveryRouteForTool(
        name: string,
        signal?: AbortSignal,
    ): Promise<JsonDiscoveryRouteForToolResult> {
        const params: JsonDiscoveryRouteForToolParams = { name };
        return this.invoke(EngineMethods.DiscoveryRouteForTool, params, signal);
    }

    // -- Agent notifications -------------------------------------------

    async agentSubagentStarted(
        sessionId: string,
        taskPrompt: string,
        signal?: AbortSignal,
    ): Promise<void> {
        const params: JsonAgentSubagentStartedParams = { sessionId, taskPrompt };
        await this.notify(EngineMethods.AgentSubagentStarted, params, signal);
    }

    async agentSubagentStopped(sessionId: string, signal?: AbortSignal): Promise<void> {
        const params: JsonAgentSubagentStoppedParams = { sessionId };
        await this.notify(EngineMethods.AgentSubagentStopped, params, signal);
    }

    async agentCompacted(sessionId: string, signal?: AbortSignal): Promise<void> {
        const params: JsonAgentCompactedParams = { sessionId };
        await this.notify(EngineMethods.AgentCompacted, params, signal);
    }

    async agentToolUsed(
        sessionId: string,
        toolName: string,
        outcome: string,
        signal?: AbortSignal,
    ): Promise<void> {
        const params: JsonAgentToolUsedParams = { sessionId, toolName, outcome };
        await this.notify(EngineMethods.AgentToolUsed, params, signal);
    }

    async agentTurnEnded(sessionId: string, signal?: AbortSignal): Promise<void> {
        const params: JsonAgentTurnEndedParams = { sessionId };
        await this.notify(EngineMethods.AgentTurnEnded, params, signal);
    }

    // -- Logs ----------------------------------------------------------

    async logsGetEngine(
        options?: { readonly lastN?: number; readonly since?: string },
        signal?: AbortSignal,
    ): Promise<JsonLogsGetEngineResult> {
        const params: JsonLogsGetEngineParams = {
            lastN: options?.lastN,
            since: options?.since,
        };
        return this.invoke(EngineMethods.LogsGetEngine, params, signal);
    }

    async logsGetWorker(
        workerId: string,
        options?: { readonly lastN?: number; readonly since?: string },
        signal?: AbortSignal,
    ): Promise<JsonLogsGetWorkerResult> {
        const params: JsonLogsGetWorkerParams = {
            workerId,
            lastN: options?.lastN,
            since: options?.since,
        };
        return this.invoke(EngineMethods.LogsGetWorker, params, signal);
    }

    // -- Subscriptions -------------------------------------------------

    /** Yields the current config snapshot, then every subsequent one. */
    async *subscribeConfig(signal?: AbortSignal): AsyncGenerator<JsonConfigSnapshot> {
        const channel = await this.openSubscription(signal);
        try {
            const frames = channel.subscribe<JsonConfigStreamFrame>(
                EngineMethods.ConfigSubscribe, undefined, signal);

            for await (const frame of frames) {
                if (frame.kind === 'dropped') {
                    throw new EngineSubscriptionDroppedError(
                        EngineMethods.ConfigSubscribe, frame.reason);
                }
                yield frame.snapshot;
            }
        }
        finally {
            await channel.dispose();
        }
    }

    /** Yields the current instructions roster, then every subsequent one. */
    async *subscribeInstructions(
        signal?: AbortSignal,
    ): AsyncGenerator<readonly JsonInstructionsListRow[]> {
        const channel = await this.openSubscription(signal);
        try {
            const frames = channel.subscribe<JsonInstructionsStreamFrame>(
                EngineMethods.InstructionsSubscribe, undefined, signal);

            for await (const frame of frames) {
                if (frame.kind === 'dropped') {
                    throw new EngineSubscriptionDroppedError(
                        EngineMethods.InstructionsSubscribe, frame.reason);
                }
                yield frame.files;
            }
        }
        finally {
            await channel.dispose();
        }
    }

    /**
     * Yields engine lifecycle transitions. These arrive on the events
     * endpoint as notifications the engine pushes after the handshake,
     * not as answers to a subscribe request.
     */
    async *subscribeLifecycle(signal?: AbortSignal): AsyncGenerator<JsonLifecycleEvent> {
        const channel = await this.openEvents(signal);
        try {
            const events = channel.notifications<JsonLifecycleEvent>(
                EngineMethods.LifecycleNotification, signal);

            for await (const event of events) {
                if (event.kind === 'dropped') {
                    throw new EngineSubscriptionDroppedError(
                        EngineMethods.LifecycleNotification, event.reason ?? 'dropped');
                }
                yield event;
            }
        }
        finally {
            await channel.dispose();
        }
    }

    /** Yields agent-loop events other clients reported to the engine. */
    async *subscribeAgentEvents(signal?: AbortSignal): AsyncGenerator<JsonAgentEvent> {
        yield* this.streamEvents<JsonAgentEvent>(EngineMethods.AgentEventsSubscribe, signal);
    }

    /** Yields engine log records as they are written. */
    async *tailEngineLogs(signal?: AbortSignal): AsyncGenerator<JsonLogRecord> {
        yield* this.streamLogRecords(EngineMethods.LogsTailEngine, undefined, signal);
    }

    /** Yields one worker's log records as they are written. */
    async *tailWorkerLogs(
        workerId: string,
        signal?: AbortSignal,
    ): AsyncGenerator<JsonLogRecord> {
        const params: JsonLogsTailWorkerParams = { workerId };
        yield* this.streamLogRecords(EngineMethods.LogsTailWorker, params, signal);
    }

    /**
     * Closes the held connection. The engine itself is left running —
     * it bounds its own lifetime through `--idle-timeout` and its
     * parent-pid watchdog.
     */
    async dispose(): Promise<void> {
        if (this.disposed) {
            return;
        }
        this.disposed = true;

        const connection = this.connection;
        this.connection = undefined;
        this.connecting = undefined;

        if (connection !== undefined) {
            await connection.dispose();
        }
    }

    private async *streamLogRecords(
        method: string,
        params: unknown,
        signal?: AbortSignal,
    ): AsyncGenerator<JsonLogRecord> {
        const channel = await this.openSubscription(signal);
        try {
            for await (const frame of channel.subscribe<JsonLogStreamFrame>(
                method, params, signal)) {
                if (frame.kind === 'dropped') {
                    throw new EngineSubscriptionDroppedError(method, frame.reason);
                }
                if (frame.kind === 'not-found') {
                    return;
                }

                yield frame.record;
            }
        }
        finally {
            await channel.dispose();
        }
    }

    private async *streamEvents<TEvent extends { readonly kind: string; readonly reason?: string }>(
        method: string,
        signal?: AbortSignal,
    ): AsyncGenerator<TEvent> {
        const channel = await this.openSubscription(signal);
        try {
            for await (const event of channel.subscribe<TEvent>(method, undefined, signal)) {
                if (event.kind === 'dropped') {
                    throw new EngineSubscriptionDroppedError(method, event.reason ?? 'dropped');
                }
                yield event;
            }
        }
        finally {
            await channel.dispose();
        }
    }

    private async openSubscription(signal?: AbortSignal): Promise<EngineSubscriptionChannel> {
        if (this.disposed) {
            throw new Error('The engine daemon manager is disposed.');
        }

        return this.connector.openSubscription(signal);
    }

    private async openEvents(signal?: AbortSignal): Promise<EngineSubscriptionChannel> {
        if (this.disposed) {
            throw new Error('The engine daemon manager is disposed.');
        }

        return this.connector.openEvents(signal);
    }

    private async invoke<TResult>(
        method: string,
        params: unknown,
        signal?: AbortSignal,
    ): Promise<TResult> {
        const connection = await this.ensureConnected();
        return connection.invoke<TResult>(method, params, signal);
    }

    private async notify(
        method: string,
        params: unknown,
        signal?: AbortSignal,
    ): Promise<void> {
        const connection = await this.ensureConnected();
        await connection.notify(method, params, signal);
    }

    private async ensureConnected(): Promise<EngineConnection> {
        if (this.disposed) {
            throw new Error('The engine daemon manager is disposed.');
        }
        if (this.connection !== undefined) {
            return this.connection;
        }

        // Concurrent first calls share one find-or-spawn round so a
        // burst of callers cannot spawn a second engine. The round takes
        // no caller signal: one participant aborting must not fail the
        // others, and the connect budget already bounds it.
        this.connecting ??= this.connector.openExchange();

        try {
            const connection = await this.connecting;

            if (this.disposed) {
                await connection.dispose();
                throw new Error('The engine daemon manager is disposed.');
            }

            this.connection = connection;
            return connection;
        }
        catch (err) {
            this.connecting = undefined;
            throw err;
        }
    }
}
