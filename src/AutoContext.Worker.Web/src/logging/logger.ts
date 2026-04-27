import { CorrelationScope } from './correlation-scope.js';
import type { LogLevel, LogRecord } from './log-record.js';
import type { LogSink } from './log-server-client.js';

/**
 * Worker-side logging facade. A `Logger` is both a leaf (carries the
 * `trace/debug/info/warn/error` methods) and a factory ({@link
 * forCategory} returns another `Logger`), so any consumer that takes
 * a `Logger` can both emit records and derive child loggers without
 * knowing the concrete implementation.
 *
 * Conceptually the same shape as the extension-side `Logger`
 * interface in `AutoContext.VsCode/src/types/logger.ts`, trimmed for
 * Worker.Web's needs: there are no per-channel concepts (no
 * `forChannel`, no `clear`) because every record flows through a
 * single shared {@link LogSink}.
 *
 * Counterpart of `Microsoft.Extensions.Logging.ILogger` on the .NET
 * side. Each call captures the active {@link CorrelationScope} id at
 * emission time so log records flowing through the shared sink carry
 * the correlation id of the dispatch that produced them.
 */
export interface Logger {
    trace(message: string, exception?: unknown): void;
    debug(message: string, exception?: unknown): void;
    info(message: string, exception?: unknown): void;
    warn(message: string, exception?: unknown): void;
    error(message: string, exception?: unknown): void;

    /**
     * Returns a logger bound to the given category. Calling
     * {@link forCategory} on a derived logger replaces (does not
     * append to) the existing category — matching the VsCode `Logger`
     * contract and `ILoggerFactory.CreateLogger` semantics.
     */
    forCategory(name: string): Logger;
}

/**
 * Default {@link Logger} implementation backed by a shared
 * {@link LogSink} (named pipe with stderr fallback). TypeScript
 * counterpart of `LogServerLogger` / `LogServerLoggerProvider` in
 * `AutoContext.Worker.Shared`.
 *
 * A single per-tree cache keyed by category name is shared between
 * the root and every logger derived through {@link forCategory}, so
 * repeated `forCategory(name)` calls — from anywhere in the tree —
 * return the same instance.
 */
export class LogServerLogger implements Logger {
    private readonly sink: LogSink;
    private readonly category: string;
    private readonly cache: Map<string, LogServerLogger>;

    constructor(
        sink: LogSink,
        category: string = '',
        cache?: Map<string, LogServerLogger>,
    ) {
        this.sink = sink;
        this.category = category;
        this.cache = cache ?? new Map();
    }

    trace(message: string, exception?: unknown): void { this.emit('Trace', message, exception); }
    debug(message: string, exception?: unknown): void { this.emit('Debug', message, exception); }
    info(message: string, exception?: unknown): void { this.emit('Information', message, exception); }
    warn(message: string, exception?: unknown): void { this.emit('Warning', message, exception); }
    error(message: string, exception?: unknown): void { this.emit('Error', message, exception); }

    forCategory(name: string): Logger {
        let cached = this.cache.get(name);
        if (cached === undefined) {
            cached = new LogServerLogger(this.sink, name, this.cache);
            this.cache.set(name, cached);
        }
        return cached;
    }

    private emit(level: LogLevel, message: string, exception?: unknown): void {
        const correlationId = CorrelationScope.current();
        const record: LogRecord = {
            category: this.category,
            level,
            message,
            ...(exception !== undefined ? { exception: formatException(exception) } : {}),
            ...(correlationId !== undefined ? { correlationId } : {}),
        };
        this.sink.enqueue(record);
    }
}

function formatException(value: unknown): string {
    if (value instanceof Error) {
        return value.stack !== undefined && value.stack.length > 0
            ? value.stack
            : `${value.name}: ${value.message}`;
    }
    if (value === null) {
        return 'null';
    }
    if (typeof value === 'object') {
        try {
            return JSON.stringify(value);
        }
        catch {
            return String(value);
        }
    }
    return String(value);
}
