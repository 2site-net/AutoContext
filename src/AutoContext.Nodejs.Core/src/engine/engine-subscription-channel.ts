import type { PipeEventsSubscriptionClient } from '../pipes/pipe-events-subscription-client.js';
import { EngineProtocolError, EngineRpcError } from './engine-errors.js';
import {
    JSON_RPC_VERSION,
    decodeFrame,
    encodeFrame,
    type JsonRpcErrorBody,
} from './json-rpc.js';

/**
 * One handshaked streaming connection to an engine. A stream
 * monopolises the connection's read side, so each stream holds a
 * dedicated channel.
 *
 * Two shapes travel over this connection. {@link subscribe} opens a
 * server-streaming request on the `rpc` endpoint and reads the stream
 * frames answering it; {@link notifications} reads what the `events`
 * endpoint pushes unprompted, with no request of its own.
 */
export class EngineSubscriptionChannel {
    private readonly client: PipeEventsSubscriptionClient;

    constructor(client: PipeEventsSubscriptionClient) {
        this.client = client;
    }

    /**
     * Opens {@link method} as a server-streaming request and yields
     * each `next` frame's result payload until the engine completes
     * the stream.
     *
     * @throws {EngineRpcError} When the engine terminates the stream
     * with an error frame, or rejects the request outright.
     * @throws {EngineProtocolError} When the engine answers with a
     * frame that is neither.
     */
    async *subscribe<TFrame>(
        method: string,
        params?: unknown,
        signal?: AbortSignal,
    ): AsyncGenerator<TFrame> {
        const request = encodeFrame({
            jsonrpc: JSON_RPC_VERSION,
            id: 1,
            method,
            params,
        });

        for await (const raw of this.client.subscribe([request], signal)) {
            const frame = decodeFrame(raw);
            const kind = frame['kind'];

            if (kind === 'next') {
                yield frame['result'] as TFrame;
                continue;
            }

            if (kind === 'complete') {
                return;
            }

            // Anything else is the engine answering the request itself
            // rather than streaming — most often a rejection. Treating
            // it as a clean end would hide the refusal entirely.
            throw toRequestError(method, frame);
        }
    }

    /**
     * Yields the params of every {@link method} notification the peer
     * pushes, until it closes the stream. Sends no request: the peer
     * starts pushing once the handshake completes.
     */
    async *notifications<TParams>(
        method: string,
        signal?: AbortSignal,
    ): AsyncGenerator<TParams> {
        for await (const raw of this.client.subscribe([], signal)) {
            const frame = decodeFrame(raw);

            if (frame['method'] !== method) {
                continue;
            }

            yield frame['params'] as TParams;
        }
    }

    /** Closes the underlying connection. Safe to call multiple times. */
    async dispose(): Promise<void> {
        await this.client.dispose();
    }
}

function toRequestError(method: string, frame: Record<string, unknown>): Error {
    const error = frame['error'];
    if (typeof error === 'object' && error !== null) {
        const body = error as JsonRpcErrorBody;
        return new EngineRpcError(method, body.code, body.message, body.data);
    }

    return new EngineProtocolError(
        `The engine answered '${method}' with an unrecognised frame.`);
}
