import type { LoggerFacade } from '#src/logging/logger-facade.js';

export interface FakeLogEntry {
    readonly level: string;
    readonly message: string;
    readonly error?: unknown;
}

export interface FakeLogger extends LoggerFacade {
    readonly logs: ReadonlyArray<FakeLogEntry>;
}
