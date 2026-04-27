import { CorrelationScope } from './correlation-scope.js';
import type { LogLevel, LogEntry } from '#types/log-entry.js';
import type { Logger } from '#types/logger.js';
import type { LogPoster } from '#types/log-poster.js';

/**
 * Default {@link Logger} implementation that hands every entry to a
 * {@link LogPoster} (typically `LoggingClient`, which delivers entries
 * over a named pipe with stderr fallback). TypeScript counterpart of
 * `PipeLogger` / `PipeLoggerProvider` in `AutoContext.Worker.Shared`.
 *
 * A single per-tree cache keyed by category name is shared between
 * the root and every logger derived through {@link forCategory}, so
 * repeated `forCategory(name)` calls — from anywhere in the tree —
 * return the same instance.
 */
export class PipeLogger implements Logger {
    private readonly poster: LogPoster;
    private readonly category: string;
    private readonly cache: Map<string, PipeLogger>;

    constructor(
        poster: LogPoster,
        category: string = '',
        cache?: Map<string, PipeLogger>,
    ) {
        this.poster = poster;
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
            cached = new PipeLogger(this.poster, name, this.cache);
            this.cache.set(name, cached);
        }
        return cached;
    }

    private emit(level: LogLevel, message: string, exception?: unknown): void {
        const correlationId = CorrelationScope.current();
        const entry: LogEntry = {
            category: this.category,
            level,
            message,
            ...(exception !== undefined ? { exception: PipeLogger.formatException(exception) } : {}),
            ...(correlationId !== undefined ? { correlationId } : {}),
        };
        this.poster.post(entry);
    }

    private static formatException(value: unknown): string {
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
}
