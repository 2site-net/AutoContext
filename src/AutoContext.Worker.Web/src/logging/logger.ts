import { LoggerBase, LogLevel as FrameworkLogLevel } from 'autocontext-framework-web';
import { CorrelationScope } from './correlation-scope.js';
import type { LogEntry, LogLevel } from '#types/log-entry.js';
import type { LogPoster } from '#types/log-poster.js';

/**
 * Default {@link LoggerBase} implementation that hands every entry to
 * a {@link LogPoster} (typically `LoggingClient`, which delivers
 * entries over a named pipe with stderr fallback). TypeScript
 * counterpart of `PipeLogger` / `PipeLoggerProvider` in
 * `AutoContext.Worker.Shared`.
 *
 * A single per-tree cache keyed by category name is shared between
 * the root and every logger derived through {@link forCategory}, so
 * repeated `forCategory(name)` calls — from anywhere in the tree —
 * return the same instance.
 *
 * Inherits `trace/debug/info/warn/error` from {@link LoggerBase}: the
 * convenience methods forward to {@link log}, which maps the shared
 * numeric {@link FrameworkLogLevel} to the .NET-compatible wire
 * string carried on every {@link LogEntry}.
 */
export class PipeLogger extends LoggerBase {
    private readonly poster: LogPoster;
    private readonly category: string;
    private readonly cache: Map<string, PipeLogger>;

    constructor(
        poster: LogPoster,
        category: string = '',
        cache?: Map<string, PipeLogger>,
    ) {
        super();
        this.poster = poster;
        this.category = category;
        this.cache = cache ?? new Map();
    }

    override log(level: FrameworkLogLevel, message: string, exception?: unknown): void {
        const wireLevel = PipeLogger.toWireLevel(level);
        if (wireLevel === undefined) {
            // `Off` is a configuration sentinel, not a call-site level — drop the record.
            return;
        }
        const correlationId = CorrelationScope.current();
        const entry: LogEntry = {
            category: this.category,
            level: wireLevel,
            message,
            ...(exception !== undefined ? { exception: PipeLogger.formatException(exception) } : {}),
            ...(correlationId !== undefined ? { correlationId } : {}),
        };
        this.poster.post(entry);
    }

    override forCategory(name: string): PipeLogger {
        let cached = this.cache.get(name);
        if (cached === undefined) {
            cached = new PipeLogger(this.poster, name, this.cache);
            this.cache.set(name, cached);
        }
        return cached;
    }

    private static toWireLevel(level: FrameworkLogLevel): LogLevel | undefined {
        switch (level) {
            case FrameworkLogLevel.Trace: return 'Trace';
            case FrameworkLogLevel.Debug: return 'Debug';
            case FrameworkLogLevel.Info:  return 'Information';
            case FrameworkLogLevel.Warn:  return 'Warning';
            case FrameworkLogLevel.Error: return 'Error';
            case FrameworkLogLevel.Off:   return undefined;
            default: {
                const exhaustive: never = level;
                throw new Error(`Unhandled log level: ${exhaustive as number}`);
            }
        }
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
