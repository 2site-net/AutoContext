/**
 * Convenience surface that fronts the narrow {@link Logger} sink with
 * the five conventional severity-named methods. Implementations
 * typically forward each method to a single underlying `log()` (see
 * {@link LoggerBase}), but the interface itself is independent — a
 * caller that only ever wants `info(...)` etc. can depend on
 * `LoggerFacade` without pulling in {@link Logger}.
 *
 * Counterpart of `Microsoft.Extensions.Logging.LoggerExtensions` on
 * the .NET side (the `LogTrace`/`LogInformation`/... convenience set).
 */
export interface LoggerFacade {
    trace(message: string, error?: unknown): void;
    debug(message: string, error?: unknown): void;
    info(message: string, error?: unknown): void;
    warn(message: string, error?: unknown): void;
    error(message: string, error?: unknown): void;
}
