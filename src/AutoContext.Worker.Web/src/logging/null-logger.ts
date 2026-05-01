import type { Logger } from '#types/logger.js';

/**
 * No-op {@link Logger} implementation. Use this at call sites that
 * legitimately have no real logger to inject — typically tests or
 * bootstrap paths that run before the real logging pipeline is wired
 * up — instead of accepting an optional logger and littering call
 * sites with `?.` checks.
 *
 * Counterpart of `Microsoft.Extensions.Logging.Abstractions.NullLogger`
 * on the .NET side. {@link NullLogger.instance} is a shared singleton;
 * {@link forCategory} returns the same instance regardless of the
 * requested category since there is nothing to write anyway.
 */
export class NullLogger implements Logger {
    public static readonly instance: NullLogger = new NullLogger();

    private constructor() {}

    trace(_message: string, _exception?: unknown): void { /* no-op */ }
    debug(_message: string, _exception?: unknown): void { /* no-op */ }
    info(_message: string, _exception?: unknown): void { /* no-op */ }
    warn(_message: string, _exception?: unknown): void { /* no-op */ }
    error(_message: string, _exception?: unknown): void { /* no-op */ }

    forCategory(_name: string): Logger {
        return this;
    }
}
