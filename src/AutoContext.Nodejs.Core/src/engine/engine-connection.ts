import type { PipeRpcExchangeClient } from '../pipes/pipe-rpc-exchange-client.js';
import { EngineProtocolError, EngineRpcError } from './engine-errors.js';
import type { JsonHandshakeResult } from '#types/json-engine-messages.js';
import {
    JSON_RPC_VERSION,
    PROTOCOL_VERSION,
    decodeFrame,
    encodeFrame,
    type JsonRpcResponse,
} from './json-rpc.js';

/** Wire method that opens every handshaked connection. */
export const ENGINE_HELLO_METHOD = 'Engine.Hello';

/**
 * One handshaked request/response connection to an engine. Requests
 * are answered in order because the underlying exchange client
 * serialises them, so an id correlates a response by construction.
 *
 * Counterpart of the C# `EngineConnection` in
 * `AutoContext.Client.Core`.
 */
export class EngineConnection {
    private readonly client: PipeRpcExchangeClient;
    private nextRequestId = 0;

    constructor(client: PipeRpcExchangeClient) {
        this.client = client;
    }

    /**
     * Runs the Engine.Hello handshake and returns the engine's
     * identity. The protocol version must match exactly.
     *
     * @throws {EngineProtocolError} When the engine refuses the
     * handshake, answers unparsably, or reports a different version.
     */
    async handshake(signal?: AbortSignal): Promise<JsonHandshakeResult> {
        let response: JsonRpcResponse;
        try {
            response = await this.exchange(
                ENGINE_HELLO_METHOD,
                { protocolVersion: PROTOCOL_VERSION },
                signal);
        }
        catch (err) {
            throw new EngineProtocolError(
                'The connection failed during the Engine.Hello handshake.',
                { cause: err });
        }

        if (response.error !== undefined) {
            throw new EngineProtocolError(
                `The engine rejected the Engine.Hello handshake: ${response.error.message}`);
        }

        const result = response.result as JsonHandshakeResult | undefined;
        if (result === undefined || result === null) {
            throw new EngineProtocolError(
                'The engine returned no result for the Engine.Hello handshake.');
        }

        if (result.protocolVersion !== PROTOCOL_VERSION) {
            throw new EngineProtocolError(
                `Protocol version mismatch: engine reports ${String(result.protocolVersion)}, `
                + `client requires ${PROTOCOL_VERSION}.`);
        }

        return result;
    }

    /**
     * Invokes {@link method} and returns its result payload.
     *
     * @throws {EngineRpcError} When the engine answers with an error.
     */
    async invoke<TResult>(
        method: string,
        params?: unknown,
        signal?: AbortSignal,
    ): Promise<TResult> {
        const response = await this.exchange(method, params, signal);

        if (response.error !== undefined) {
            throw new EngineRpcError(
                method,
                response.error.code,
                response.error.message,
                response.error.data);
        }

        return response.result as TResult;
    }

    /**
     * Fires {@link method} as a JSON-RPC notification. The engine sends
     * no response, so delivery is not confirmed beyond the write.
     */
    async notify(method: string, params?: unknown, signal?: AbortSignal): Promise<void> {
        const frame = encodeFrame({ jsonrpc: JSON_RPC_VERSION, method, params });
        await this.client.send(frame, signal);
    }

    /** Closes the underlying connection. Safe to call multiple times. */
    async dispose(): Promise<void> {
        await this.client.dispose();
    }

    private async exchange(
        method: string,
        params: unknown,
        signal?: AbortSignal,
    ): Promise<JsonRpcResponse> {
        this.nextRequestId += 1;

        const request = encodeFrame({
            jsonrpc: JSON_RPC_VERSION,
            id: this.nextRequestId,
            method,
            params,
        });

        const response = await this.client.exchange(request, signal);
        return decodeFrame(response) as unknown as JsonRpcResponse;
    }
}
