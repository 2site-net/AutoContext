import type { FakeLogEntry, FakeLogger } from './fake-logger.js';

export class FakeLoggerFactory {
    public static create(): FakeLogger {
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
}
