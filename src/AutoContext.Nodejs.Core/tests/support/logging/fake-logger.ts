import type { LoggerFacade } from '#types/logger-facade.js';

export interface FakeLogEntry {
    readonly level: string;
    readonly message: string;
    readonly error?: unknown;
}

export interface FakeLogger extends LoggerFacade {
    readonly logs: ReadonlyArray<FakeLogEntry>;
}

/** Build an in-memory `LoggerFacade` that records every log call into `logs`. */
export function createFakeLogger(): FakeLogger {
    const logs: FakeLogEntry[] = [];
    const make = (level: string) => (message: string, error?: unknown): void => {
        logs.push({ level, message, error });
    };
    return {
        logs,
        trace: make('trace'),
        debug: make('debug'),
        info: make('info'),
        warn: make('warn'),
        error: make('error'),
    };
}
