import { CorrelationScope } from './correlation-scope.js';
import type { LogLevel, LogRecord } from './log-record.js';
import type { LogSink } from './log-server-client.js';

/**
 * Per-category logger handed out by {@link Logger.forCategory}. Every
 * call captures the active {@link CorrelationScope} id at emission
 * time so log records flowing through the shared sink carry the
 * correlation id of the dispatch that produced them.
 */
export interface CategoryLogger {
    trace(message: string, exception?: unknown): void;
    debug(message: string, exception?: unknown): void;
    info(message: string, exception?: unknown): void;
    warn(message: string, exception?: unknown): void;
    error(message: string, exception?: unknown): void;
}

/**
 * Worker-side logger facade. Hands out per-category loggers, all of
 * which enqueue records onto the shared {@link LogSink}. TypeScript
 * counterpart of `LogServerLoggerProvider` + `LogServerLogger` in
 * `AutoContext.Worker.Shared`.
 */
export class Logger {
    private readonly cache = new Map<string, CategoryLogger>();

    constructor(private readonly sink: LogSink) {}

    forCategory(name: string): CategoryLogger {
        let cached = this.cache.get(name);
        if (cached === undefined) {
            cached = createCategoryLogger(name, this.sink);
            this.cache.set(name, cached);
        }
        return cached;
    }
}

function createCategoryLogger(category: string, sink: LogSink): CategoryLogger {
    const emit = (level: LogLevel, message: string, exception?: unknown): void => {
        const record: LogRecord = {
            category,
            level,
            message,
            ...(exception !== undefined ? { exception: formatException(exception) } : {}),
            ...(CorrelationScope.current() !== undefined
                ? { correlationId: CorrelationScope.current() }
                : {}),
        };
        sink.enqueue(record);
    };

    return {
        trace: (m, e) => emit('Trace', m, e),
        debug: (m, e) => emit('Debug', m, e),
        info: (m, e) => emit('Information', m, e),
        warn: (m, e) => emit('Warning', m, e),
        error: (m, e) => emit('Error', m, e),
    };
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
