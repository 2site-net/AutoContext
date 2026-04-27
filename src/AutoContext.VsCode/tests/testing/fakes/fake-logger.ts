import { vi } from 'vitest';
import type { Logger } from '#types/logger.js';

/**
 * Returns a `Logger` whose level methods are all `vi.fn()` spies.
 * `forCategory` and `forChannel` both return the same fake (so child
 * loggers share the same spies, regardless of which channel/category
 * they were derived for). `clear` is also a spy. Tests assert on
 * `logger.error('msg', err)`, `logger.clear()`, etc. directly.
 */
export function createFakeLogger(): Logger {
    const logger: Logger = {
        trace: vi.fn(),
        debug: vi.fn(),
        info: vi.fn(),
        warn: vi.fn(),
        error: vi.fn(),
        clear: vi.fn(),
        forCategory: vi.fn(() => logger),
        forChannel: vi.fn(() => logger),
    };
    return logger;
}
