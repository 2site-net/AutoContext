import type { LogEntry } from '#types/log-entry.js';
import type { LogPoster } from '#types/log-poster.js';

/**
 * Test double for `LoggingClient` that captures every posted entry
 * in {@link records} for later assertion. Implements {@link LogPoster}
 * so it slots straight into `new PipeLogger(...)`.
 */
export class FakeLoggingClient implements LogPoster {
    readonly records: LogEntry[] = [];

    post(entry: LogEntry): void {
        this.records.push(entry);
    }
}
