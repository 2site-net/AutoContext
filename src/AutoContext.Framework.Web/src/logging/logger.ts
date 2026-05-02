import type { LogLevel } from './log-level.js';

/**
 * Narrow logging sink: a single `log()` method that accepts a level,
 * a message, and an optional error. The minimum contract for "thing
 * that consumes log records."
 *
 * This interface is deliberately the sibling — not the parent — of
 * {@link LoggerFacade}: a consumer that only needs to *forward*
 * records (a transport, a wrapper that adds correlation IDs, an
 * in-memory test spy) depends on `Logger`; a consumer that wants the
 * five-level convenience surface depends on `LoggerFacade`.
 *
 * {@link LoggerBase} bridges the two by implementing both interfaces
 * and forwarding the convenience methods to `log()`. Counterpart of
 * `Microsoft.Extensions.Logging.ILogger.Log(...)` on the .NET side.
 */
export interface Logger {
    log(level: LogLevel, message: string, error?: unknown): void;
}
