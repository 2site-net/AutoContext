import { vi } from 'vitest';
import type { Logger } from '../../../src/types/logger';

/**
 * Returns a `Logger` whose level methods are all `vi.fn()` spies and whose
 * `forCategory` returns the same fake (so child loggers share the same
 * spies). Tests assert on `logger.error('msg', err)` etc. directly.
 */
export function createFakeLogger(): Logger {
    const logger: Logger = {
        trace: vi.fn(),
        debug: vi.fn(),
        info: vi.fn(),
        warn: vi.fn(),
        error: vi.fn(),
        forCategory: vi.fn(() => logger),
    };
    return logger;
}
