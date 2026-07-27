import type { Socket } from 'node:net';

import type { LoggerFacade } from '../logging/logger-facade.js';
import { LengthPrefixedFrameCodec } from './length-prefixed-frame-codec.js';
import { PipeTransport } from './pipe-transport.js';

/**
 * Construction options for {@link PipeRpcExchangeClient}.
 */
export interface PipeRpcExchangeClientOptions {
    /** Transport used to dial the pipe. */
    readonly transport: PipeTransport;

    /** Pipe name to dial, without the platform prefix. */
    readonly pipeName: string;

    /** Sink for connect and teardown diagnostics. */
    readonly logger: LoggerFacade;

    /** Milliseconds to wait for the connect. Defaults to 2000. */
    readonly connectTimeoutMs?: number;
}

/**
 * Layer-3 request/response client over one framed pipe connection.
 * Each {@link exchange} writes a request frame and reads the frame the
 * peer answers with; concurrent callers are serialised so a response is
 * never handed to the wrong request.
 *
 * The connection is opened on first use and held until {@link dispose}.
 * An I/O failure faults the client permanently rather than silently
 * reconnecting: a peer that requires a handshake at the head of the
 * connection would otherwise be handed an unhandshaked one, and the
 * caller — which owns that handshake — would never know.
 *
 * Counterpart of the C# `PipePersistentExchangeClient` in
 * `AutoContext.Framework.Pipes`.
 */
export class PipeRpcExchangeClient {
    private static readonly DEFAULT_CONNECT_TIMEOUT_MS = 2000;

    private readonly connectTimeoutMs: number;
    private readonly logger: LoggerFacade;
    private readonly pipeName: string;
    private readonly transport: PipeTransport;

    private codec: LengthPrefixedFrameCodec | undefined;
    private disposed = false;
    private faulted: Error | undefined;
    private gate: Promise<unknown> = Promise.resolve();
    private socket: Socket | undefined;

    constructor(options: PipeRpcExchangeClientOptions) {
        if (options.pipeName === '') {
            throw new Error('pipeName must not be empty.');
        }

        this.transport = options.transport;
        this.pipeName = options.pipeName;
        this.logger = options.logger;
        this.connectTimeoutMs = options.connectTimeoutMs ?? PipeRpcExchangeClient.DEFAULT_CONNECT_TIMEOUT_MS;
    }

    /**
     * Writes {@link request} as one frame and resolves with the frame
     * the peer answers with. Calls queue behind one another, so the
     * pairing of request to response holds under concurrency.
     *
     * @throws When the client is disposed, has already faulted, the
     * peer closes without answering, or the signal aborts.
     */
    exchange(request: Buffer | Uint8Array, signal?: AbortSignal): Promise<Buffer> {
        const attempt = this.gate.then(
            () => this.exchangeCore(request, signal),
            () => this.exchangeCore(request, signal));

        this.gate = attempt.catch(() => undefined);
        return attempt;
    }

    /**
     * Closes the held connection, releasing any in-flight exchange.
     * Safe to call multiple times.
     */
    async dispose(): Promise<void> {
        if (this.disposed) {
            return;
        }
        this.disposed = true;

        await this.closeConnection();
    }

    private async exchangeCore(request: Buffer | Uint8Array, signal?: AbortSignal): Promise<Buffer> {
        if (this.disposed) {
            throw new Error(`Exchange client for '${this.pipeName}' is disposed.`);
        }
        if (this.faulted !== undefined) {
            throw this.faulted;
        }

        const codec = await this.ensureConnected(signal);

        try {
            await codec.write(Buffer.from(request), signal);

            const response = await codec.read(signal);
            if (this.disposed) {
                throw new Error(`Exchange client for '${this.pipeName}' was disposed mid-request.`);
            }
            if (response === null) {
                throw new Error(`Peer closed '${this.pipeName}' before answering the request.`);
            }

            return response;
        }
        catch (err) {
            this.faulted = err instanceof Error ? err : new Error(String(err));
            await this.closeConnection();
            throw err;
        }
    }

    private async ensureConnected(signal?: AbortSignal): Promise<LengthPrefixedFrameCodec> {
        if (this.codec !== undefined) {
            return this.codec;
        }

        const deadline = AbortSignal.timeout(this.connectTimeoutMs);
        const linked = signal === undefined ? deadline : AbortSignal.any([signal, deadline]);

        try {
            const socket = await this.transport.connect(this.pipeName, linked);

            if (this.disposed) {
                socket.destroy();
                throw new Error(`Exchange client for '${this.pipeName}' is disposed.`);
            }

            // A late socket error surfaces on the pending read or write;
            // the listener keeps it off the process-level error path.
            socket.on('error', () => { /* see class doc */ });

            this.socket = socket;
            this.codec = new LengthPrefixedFrameCodec(socket);
            return this.codec;
        }
        catch (err) {
            this.faulted = err instanceof Error ? err : new Error(String(err));
            throw err;
        }
    }

    private async closeConnection(): Promise<void> {
        const socket = this.socket;
        this.socket = undefined;
        this.codec = undefined;

        if (socket === undefined) {
            return;
        }

        this.logger.debug(`Closing exchange connection to '${this.pipeName}'.`);

        await new Promise<void>((resolve) => {
            socket.once('close', () => resolve());
            socket.destroy();
        });
    }
}
