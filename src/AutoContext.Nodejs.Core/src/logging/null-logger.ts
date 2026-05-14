import type { LogCategory } from './log-category.js';
import type { LogLevel } from './log-level.js';
import { LoggerBase } from './logger-base.js';

/**
 * No-op logger implementation. Use for silent paths and tests where
 * logging output would be noise. Satisfies {@link Logger} and
 * {@link LoggerFacade} via {@link LoggerBase}. Counterpart of
 * `Microsoft.Extensions.Logging.Abstractions.NullLogger` on the .NET
 * side.
 */
export class NullLogger extends LoggerBase {
    static readonly instance: NullLogger = new NullLogger();

    override log(_level: LogLevel, _message: string, _error?: unknown): void {
        // intentionally empty
    }

    override forCategory(_category: LogCategory | string): LoggerBase {
        return this;
    }
}
