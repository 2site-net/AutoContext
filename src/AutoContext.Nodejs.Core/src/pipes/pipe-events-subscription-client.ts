import type { Socket } from 'node:net';

import type { LoggerFacade } from '#types/logger-facade.js';
import { LengthPrefixedFrameCodec } from './length-prefixed-frame-codec.js';
import { PipeTransport } from './pipe-transport.js';

/**
 * Writes one request frame on the freshly-connected pipe and resolves
 * with the frame the peer answers with.
 */
export type PipeFrameExchange = (request: Buffer | Uint8Array) => Promise<Buffer>;

/**
 * Construction options for {@link PipeEventsSubscriptionClient}.
 */
export interface PipeEventsSubscriptionClientOptions {
    /** Transport used to dial the pipe. */
    readonly transport: PipeTransport;

    /** Pipe name to dial, without the platform prefix. */
    readonly pipeName: string;

    /** Sink for connect and teardown diagnostics. */
    readonly logger: LoggerFacade;

    /**
     * Runs once on the fresh connection, before any subscribe frame is
     * written, for peers that gate a subscription behind a
     * request/response handshake. Rejecting aborts the subscription.
     */
    readonly handshake?: (exchange: PipeFrameExchange) => Promise<void>;

    /** Milliseconds to wait for the connect. Defaults to 2000. */
    readonly connectTimeoutMs?: number;
}

/**
 * Layer-3 subscription client over one framed pipe connection. Opens
 * the pipe, writes the caller's subscribe frames, and yields every
 * frame the peer pushes until it closes the stream or the caller
 * disposes.
 *
 * The stream is inbound-only once subscribed: the peer decides when a
 * frame arrives, so a consumer that stops pulling applies no
 * backpressure to the producer — the peer drops it instead, per the
 * engine's slow-subscriber contract.
 *
 * Counterpart of the C# subscription path on `EngineConnection` in
 * `AutoContext.Client.Core`.
 */
export class PipeEventsSubscriptionClient {
    private static readonly DEFAULT_CONNECT_TIMEOUT_MS = 2000;

    private readonly connectTimeoutMs: number;
    private readonly handshake: ((exchange: PipeFrameExchange) => Promise<void>) | undefined;
    private readonly logger: LoggerFacade;
    private readonly pipeName: string;
    private readonly transport: PipeTransport;

    private codec: LengthPrefixedFrameCodec | undefined;
    private disposed = false;
    private socket: Socket | undefined;

    constructor(options: PipeEventsSubscriptionClientOptions) {
        if (options.pipeName === '') {
            throw new Error('pipeName must not be empty.');
        }

        this.transport = options.transport;
        this.pipeName = options.pipeName;
        this.logger = options.logger;
        this.handshake = options.handshake;
        this.connectTimeoutMs = options.connectTimeoutMs
            ?? PipeEventsSubscriptionClient.DEFAULT_CONNECT_TIMEOUT_MS;
    }

    /**
     * Connects, writes each frame in {@link subscribeFrames} in order,
     * then yields inbound frames until the peer closes the stream, the
     * signal aborts, or the client is disposed.
     *
     * @throws When the client is disposed or already subscribed, the
     * connect fails, or the signal aborts.
     */
    async *subscribe(
        subscribeFrames: readonly (Buffer | Uint8Array)[],
        signal?: AbortSignal,
    ): AsyncGenerator<Buffer> {
        if (this.disposed) {
            throw new Error(`Subscription client for '${this.pipeName}' is disposed.`);
        }

        const codec = await this.connect(signal);

        if (this.handshake !== undefined) {
            await this.handshake(async (request) => {
                await codec.write(Buffer.from(request), signal);

                const response = await codec.read(signal);
                if (response === null) {
                    throw new Error(
                        `Peer closed '${this.pipeName}' during the subscription handshake.`);
                }

                return response;
            });
        }

        for (const frame of subscribeFrames) {
            await codec.write(Buffer.from(frame), signal);
        }

        for (;;) {
            const frame = await codec.read(signal);

            if (this.disposed) {
                return;
            }

            if (frame === null) {
                this.logger.debug(`Peer closed the subscription stream on '${this.pipeName}'.`);
                return;
            }

            yield frame;
        }
    }

    /**
     * Closes the held connection, ending any in-flight subscription.
     * Safe to call multiple times.
     */
    async dispose(): Promise<void> {
        if (this.disposed) {
            return;
        }
        this.disposed = true;

        const socket = this.socket;
        this.socket = undefined;
        this.codec = undefined;

        if (socket === undefined) {
            return;
        }

        this.logger.debug(`Closing subscription connection to '${this.pipeName}'.`);

        await new Promise<void>((resolve) => {
            socket.once('close', () => resolve());
            socket.destroy();
        });
    }

    private async connect(signal?: AbortSignal): Promise<LengthPrefixedFrameCodec> {
        if (this.codec !== undefined) {
            throw new Error(`Subscription client for '${this.pipeName}' is already subscribed.`);
        }

        const deadline = AbortSignal.timeout(this.connectTimeoutMs);
        const linked = signal === undefined ? deadline : AbortSignal.any([signal, deadline]);
        const socket = await this.transport.connect(this.pipeName, linked);

        if (this.disposed) {
            socket.destroy();
            throw new Error(`Subscription client for '${this.pipeName}' is disposed.`);
        }

        // A late socket error surfaces on the pending read; the
        // listener keeps it off the process-level error path.
        socket.on('error', () => { /* see class doc */ });

        this.socket = socket;
        this.codec = new LengthPrefixedFrameCodec(socket);
        return this.codec;
    }
}
