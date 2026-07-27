/** JSON-RPC envelope version every frame carries. */
export const JSON_RPC_VERSION = '2.0';

/** Protocol version the Engine.Hello handshake must agree on. */
export const PROTOCOL_VERSION = 1;

/** Request frame written to the engine. */
export interface JsonRpcRequest {
    readonly jsonrpc: string;
    /** Absent on notifications, which the engine never answers. */
    readonly id?: number;
    readonly method: string;
    readonly params?: unknown;
}

/** Error object carried by a failed response. */
export interface JsonRpcErrorBody {
    readonly code: number;
    readonly message: string;
    readonly data?: unknown;
}

/** Response frame the engine answers a request with. */
export interface JsonRpcResponse {
    readonly jsonrpc: string;
    readonly id: number | string | null;
    readonly result?: unknown;
    readonly error?: JsonRpcErrorBody;
}

/** Serialises {@link request} as the UTF-8 JSON bytes of one frame. */
export function encodeFrame(request: JsonRpcRequest): Buffer {
    return Buffer.from(JSON.stringify(request), 'utf8');
}

/**
 * Parses one frame's UTF-8 JSON bytes.
 *
 * @throws When the payload is not a JSON object.
 */
export function decodeFrame(payload: Buffer): Record<string, unknown> {
    let parsed: unknown;
    try {
        parsed = JSON.parse(payload.toString('utf8'));
    }
    catch (err) {
        const message = err instanceof Error ? err.message : String(err);
        throw new Error(`The engine sent an unparsable frame: ${message}`);
    }

    if (typeof parsed !== 'object' || parsed === null || Array.isArray(parsed)) {
        throw new Error('The engine sent a frame that is not a JSON object.');
    }

    return parsed as Record<string, unknown>;
}
