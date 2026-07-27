import { PipeEventsSubscriptionClient } from '../pipes/pipe-events-subscription-client.js';
import { PipeRpcExchangeClient } from '../pipes/pipe-rpc-exchange-client.js';
import type { PipeTransport } from '../pipes/pipe-transport.js';
import type { LoggerFacade } from '#types/logger-facade.js';
import { EndpointKind, formatEndpoint } from './endpoint.js';
import {
    DEFAULT_ENGINE_CONNECT_BUDGET,
    nextRetryDelayMs,
    type EngineConnectBudget,
} from './engine-connect-budget.js';
import { EngineConnection } from './engine-connection.js';
import { EngineProtocolError, EngineUnavailableError } from './engine-errors.js';
import type { EngineSpawner } from './engine-spawner.js';
import { EngineSubscriptionChannel } from './engine-subscription-channel.js';
import { PROTOCOL_VERSION, JSON_RPC_VERSION, decodeFrame, encodeFrame } from './json-rpc.js';
import { ENGINE_HELLO_METHOD } from './engine-connection.js';

/** Construction options for {@link EngineConnector}. */
export interface EngineConnectorOptions {
    readonly transport: PipeTransport;
    readonly spawner: EngineSpawner;
    readonly logger: LoggerFacade;

    /** Absolute workspace path this connector resolves an engine for. */
    readonly workspacePath: string;

    /** 16-uppercase-hex hash of {@link workspacePath}. */
    readonly workspaceHash: string;

    /** Per-launch UUID identifying the engine instance. */
    readonly instanceId: string;

    /** Absolute path of the engine binary to spawn. */
    readonly engineBinaryPath: string;

    /** Descriptor forwarded on `--instance-label` when spawning. */
    readonly instanceLabel?: string;

    /** Whole seconds forwarded on `--idle-timeout` when spawning. */
    readonly idleTimeoutSeconds?: number;

    /** When true the connector dials only and never spawns. */
    readonly spawnDisabled?: boolean;

    /** Connect timing. Defaults to {@link DEFAULT_ENGINE_CONNECT_BUDGET}. */
    readonly budget?: EngineConnectBudget;
}

/**
 * Resolves a live engine for one workspace: dials the endpoint, and
 * when nothing answers, spawns an engine and retries within the
 * connect budget.
 *
 * Counterpart of the C# `EngineConnector` in `AutoContext.Client.Core`.
 */
export class EngineConnector {
    private readonly budget: EngineConnectBudget;
    private readonly options: EngineConnectorOptions;

    constructor(options: EngineConnectorOptions) {
        this.options = options;
        this.budget = options.budget ?? DEFAULT_ENGINE_CONNECT_BUDGET;
    }

    /** Endpoint address of this connector's `rpc` pipe. */
    get rpcAddress(): string {
        return formatEndpoint(
            EndpointKind.Rpc,
            this.options.workspaceHash,
            this.options.instanceId);
    }

    /** Endpoint address of this connector's `events` pipe. */
    get eventsAddress(): string {
        return formatEndpoint(
            EndpointKind.Events,
            this.options.workspaceHash,
            this.options.instanceId);
    }

    /**
     * Returns a handshaked request/response connection, spawning an
     * engine first when none is listening.
     *
     * @throws {EngineUnavailableError} When no engine could be reached.
     * @throws {EngineProtocolError} When an engine answered on an
     * incompatible protocol version.
     */
    async openExchange(signal?: AbortSignal): Promise<EngineConnection> {
        return this.resolve(
            (connectTimeoutMs) => this.dialExchange(connectTimeoutMs, signal),
            signal);
    }

    /**
     * Ensures an engine is reachable, then opens a dedicated
     * server-streaming channel on the `rpc` endpoint.
     */
    async openSubscription(signal?: AbortSignal): Promise<EngineSubscriptionChannel> {
        const probe = await this.openExchange(signal);
        await probe.dispose();

        return this.openChannel(this.rpcAddress);
    }

    /**
     * Ensures an engine is reachable, then opens a channel on the
     * `events` endpoint, which pushes notifications rather than
     * answering requests.
     */
    async openEvents(signal?: AbortSignal): Promise<EngineSubscriptionChannel> {
        const probe = await this.openExchange(signal);
        await probe.dispose();

        return this.openChannel(this.eventsAddress);
    }

    private openChannel(address: string): EngineSubscriptionChannel {
        const client = new PipeEventsSubscriptionClient({
            transport: this.options.transport,
            pipeName: address,
            logger: this.options.logger,
            connectTimeoutMs: this.budget.coldConnectAttemptTimeoutMs,
            handshake: async (exchange) => {
                const request = encodeFrame({
                    jsonrpc: JSON_RPC_VERSION,
                    id: 0,
                    method: ENGINE_HELLO_METHOD,
                    params: { protocolVersion: PROTOCOL_VERSION },
                });

                const reported = readProtocolVersion(decodeFrame(await exchange(request)));
                if (reported !== PROTOCOL_VERSION) {
                    throw new EngineProtocolError(
                        `Protocol version mismatch: engine reports ${String(reported)}, `
                        + `client requires ${PROTOCOL_VERSION}.`);
                }
            },
        });

        return new EngineSubscriptionChannel(client);
    }

    private async resolve(
        dial: (connectTimeoutMs: number) => Promise<EngineConnection>,
        signal?: AbortSignal,
    ): Promise<EngineConnection> {
        const warm = await this.tryDial(dial, this.budget.warmConnectTimeoutMs, signal);
        if (warm !== null) {
            return warm;
        }

        const address = this.rpcAddress;

        if (this.options.spawnDisabled === true) {
            throw new EngineUnavailableError(
                `No engine is listening on '${address}' and spawning is disabled.`);
        }

        this.options.spawner.spawn({
            workspacePath: this.options.workspacePath,
            instanceId: this.options.instanceId,
            instanceLabel: this.options.instanceLabel,
            idleTimeoutSeconds: this.options.idleTimeoutSeconds,
            engineBinaryPath: this.options.engineBinaryPath,
        });

        const cold = await this.retryDial(dial, signal);
        if (cold !== null) {
            return cold;
        }

        throw new EngineUnavailableError(
            `Spawned an engine but it did not begin accepting connections on '${address}' `
            + `within ${this.budget.coldConnectBudgetMs}ms.`);
    }

    private async retryDial(
        dial: (connectTimeoutMs: number) => Promise<EngineConnection>,
        signal?: AbortSignal,
    ): Promise<EngineConnection | null> {
        const deadline = Date.now() + this.budget.coldConnectBudgetMs;
        let delayMs = 0;

        while (Date.now() < deadline) {
            delayMs = nextRetryDelayMs(this.budget, delayMs);
            await delay(delayMs, signal);

            const connection = await this.tryDial(
                dial, this.budget.coldConnectAttemptTimeoutMs, signal);
            if (connection !== null) {
                return connection;
            }
        }

        return null;
    }

    private async tryDial(
        dial: (connectTimeoutMs: number) => Promise<EngineConnection>,
        connectTimeoutMs: number,
        signal?: AbortSignal,
    ): Promise<EngineConnection | null> {
        try {
            return await dial(connectTimeoutMs);
        }
        catch (err) {
            // An engine that answered on the wrong protocol version is
            // present, not absent — retrying would never succeed.
            if (err instanceof EngineProtocolError) {
                throw err;
            }
            if (signal?.aborted === true) {
                throw err;
            }

            const message = err instanceof Error ? err.message : String(err);
            this.options.logger.debug(
                `Engine connect attempt on '${this.rpcAddress}' failed: ${message}`);
            return null;
        }
    }

    private async dialExchange(
        connectTimeoutMs: number,
        signal?: AbortSignal,
    ): Promise<EngineConnection> {
        const client = new PipeRpcExchangeClient({
            transport: this.options.transport,
            pipeName: this.rpcAddress,
            logger: this.options.logger,
            connectTimeoutMs,
        });

        // Connect before handshaking so a pipe nothing is listening on
        // stays distinguishable from an engine that answered badly.
        try {
            await client.connect(signal);
        }
        catch (err) {
            await client.dispose();
            throw err;
        }

        const connection = new EngineConnection(client);
        try {
            await connection.handshake(signal);
            return connection;
        }
        catch (err) {
            await connection.dispose();
            throw err;
        }
    }
}

function readProtocolVersion(response: Record<string, unknown>): number | undefined {
    const result = response['result'];
    if (typeof result !== 'object' || result === null) {
        return undefined;
    }

    const reported = (result as Record<string, unknown>)['protocolVersion'];
    return typeof reported === 'number' ? reported : undefined;
}

function delay(milliseconds: number, signal?: AbortSignal): Promise<void> {
    return new Promise<void>((resolve, reject) => {
        if (signal?.aborted === true) {
            reject(new Error('The operation was aborted.'));
            return;
        }

        let timer: ReturnType<typeof setTimeout>;

        const onAbort = (): void => {
            clearTimeout(timer);
            reject(new Error('The operation was aborted.'));
        };

        timer = setTimeout(() => {
            signal?.removeEventListener('abort', onAbort);
            resolve();
        }, milliseconds);

        signal?.addEventListener('abort', onAbort, { once: true });
    });
}
