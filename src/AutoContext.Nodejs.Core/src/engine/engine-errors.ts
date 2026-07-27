/**
 * Raised when no engine could be reached — nothing was listening and
 * spawning is disabled, the binary failed to start, or a spawned
 * engine never began accepting inside the connect budget.
 */
export class EngineUnavailableError extends Error {
    constructor(message: string, options?: ErrorOptions) {
        super(message, options);
        this.name = 'EngineUnavailableError';
    }
}

/**
 * Raised when an engine answered but the connection could not be
 * established on the agreed protocol — a refused or unparsable
 * Engine.Hello, or a protocol-version mismatch.
 */
export class EngineProtocolError extends Error {
    constructor(message: string, options?: ErrorOptions) {
        super(message, options);
        this.name = 'EngineProtocolError';
    }
}

/**
 * Raised when the engine answers a request with a JSON-RPC error
 * object.
 */
export class EngineRpcError extends Error {
    /** JSON-RPC error code reported by the engine. */
    readonly code: number;

    /** Wire method that failed. */
    readonly method: string;

    /** Structured payload the engine attached to the error, if any. */
    readonly data: unknown;

    constructor(method: string, code: number, message: string, data?: unknown) {
        super(`The engine rejected '${method}' (${code}): ${message}`);
        this.name = 'EngineRpcError';
        this.method = method;
        this.code = code;
        this.data = data;
    }
}

/**
 * Raised when the engine drops this subscriber for falling behind and
 * terminates the stream with a `dropped` frame.
 */
export class EngineSubscriptionDroppedError extends Error {
    /** Wire method whose stream was dropped. */
    readonly method: string;

    /** Engine-reported drop reason, e.g. `slow-subscriber`. */
    readonly reason: string;

    constructor(method: string, reason: string) {
        super(`The engine dropped the '${method}' subscription: ${reason}.`);
        this.name = 'EngineSubscriptionDroppedError';
        this.method = method;
        this.reason = reason;
    }
}
