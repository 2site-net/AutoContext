import type { LoggerFacade } from '#src/logging/logger-facade.js';

interface LogEntry { level: string; message: string; error?: unknown }

export interface FakeLogger extends LoggerFacade {
    readonly logs: ReadonlyArray<LogEntry>;
}

export function createFakeLogger(): FakeLogger {
    const logs: LogEntry[] = [];
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

let counter = 0;
export function uniquePipeName(): string {
    counter += 1;
    const random = Math.random().toString(36).slice(2, 8);
    return `autocontext-test-${process.pid}-${Date.now()}-${counter}-${random}`;
}

export async function until(predicate: () => boolean, timeoutMs = 2000, stepMs = 10): Promise<void> {
    const start = Date.now();
    while (!predicate()) {
        if (Date.now() - start > timeoutMs) {
            throw new Error('until: timed out');
        }
        await new Promise<void>((resolve) => setTimeout(resolve, stepMs));
    }
}
