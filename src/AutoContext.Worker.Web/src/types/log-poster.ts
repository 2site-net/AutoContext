import type { LogEntry } from './log-entry.js';

/**
 * Anything that can accept a {@link LogEntry} for delivery. Decouples
 * `PipeLogger` from the concrete `LoggingClient` so test doubles can
 * stand in without inheriting its private I/O state.
 */
export interface LogPoster {
    post(entry: LogEntry): void;
}
