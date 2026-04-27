import type { LogSink } from './log-sink.js';

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
 * side. Each call captures the active `CorrelationScope` id at
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
