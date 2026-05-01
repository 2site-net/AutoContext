import type { Logger } from './logger.js';

/**
 * No-op {@link Logger} implementation. Use for silent paths and tests
 * where logging output would be noise. Counterpart of
 * `Microsoft.Extensions.Logging.Abstractions.NullLogger` on the .NET
 * side.
 */
export class NullLogger implements Logger {
    static readonly instance: Logger = new NullLogger();

    trace(_message: string, _exception?: unknown): void {
        // intentionally empty
    }

    debug(_message: string, _exception?: unknown): void {
        // intentionally empty
    }

    info(_message: string, _exception?: unknown): void {
        // intentionally empty
    }

    warn(_message: string, _exception?: unknown): void {
        // intentionally empty
    }

    error(_message: string, _exception?: unknown): void {
        // intentionally empty
    }
}
