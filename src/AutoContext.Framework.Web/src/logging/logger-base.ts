import type { LogCategory } from './log-category.js';
import { LogLevel } from './log-level.js';
import type { LoggerFacade } from './logger-facade.js';
import type { Logger } from './logger.js';

/**
 * Abstract base that unifies {@link Logger} and {@link LoggerFacade}.
 * Subclasses override the abstract `log()` and `forCategory()`; the
 * five level conveniences are inherited as forwarders to `log()`.
 *
 * `forCategory()` is declared to return `LoggerBase` so chained calls
 * (`logger.forCategory(...).forCategory(...)`) typecheck without
 * needing a separate `CategoryLogger` interface to anchor the
 * recursion. Concrete logger classes that want to participate in
 * category derivation must extend `LoggerBase` (or a descendant such
 * as {@link ChannelLoggerBase}).
 *
 * Extending `LoggerBase` gives a class the narrow sink contract (so
 * it can be wrapped/composed) and the level-method facade — without
 * forcing any single consumer's *parameter* type to also be the
 * union.
 */
export abstract class LoggerBase implements Logger, LoggerFacade {
    abstract log(level: LogLevel, message: string, error?: unknown): void;
    abstract forCategory(category: LogCategory | string): LoggerBase;

    trace(message: string, error?: unknown): void {
        this.log(LogLevel.Trace, message, error);
    }

    debug(message: string, error?: unknown): void {
        this.log(LogLevel.Debug, message, error);
    }

    info(message: string, error?: unknown): void {
        this.log(LogLevel.Info, message, error);
    }

    warn(message: string, error?: unknown): void {
        this.log(LogLevel.Warn, message, error);
    }

    error(message: string, error?: unknown): void {
        this.log(LogLevel.Error, message, error);
    }
}
