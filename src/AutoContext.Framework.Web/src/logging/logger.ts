/**
 * Minimal logging facade for plumbing primitives in
 * `AutoContext.Framework.Web`. Intentionally a subset of richer
 * project-specific loggers (e.g. `AutoContext.Worker.Web/types/logger`,
 * `AutoContext.VsCode/types/logger`) so that any of those satisfies
 * this shape structurally and can be passed to a transport primitive
 * without an adapter.
 *
 * Counterpart of `Microsoft.Extensions.Logging.ILogger` on the .NET
 * side. Implementations are responsible for any correlation-id
 * capture, level filtering, and sink selection.
 */
export interface Logger {
    trace(message: string, exception?: unknown): void;
    debug(message: string, exception?: unknown): void;
    info(message: string, exception?: unknown): void;
    warn(message: string, exception?: unknown): void;
    error(message: string, exception?: unknown): void;
}
