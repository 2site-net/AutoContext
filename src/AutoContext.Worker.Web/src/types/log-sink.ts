import type { LogRecord } from './log-record.js';

/**
 * Sink that accepts {@link LogRecord} values for delivery. The
 * {@link Logger} facade depends on this surface only, so tests can
 * substitute a buffer without spinning up a pipe.
 */
export interface LogSink {
    enqueue(record: LogRecord): void;
}
