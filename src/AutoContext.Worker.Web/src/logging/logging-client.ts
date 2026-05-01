import { NullLogger, PipeStreamingClient, PipeTransport } from 'autocontext-framework-web';

import type { JsonLogEntry, JsonLogGreeting, LogEntry } from '#types/log-entry.js';
import type { LogPoster } from '#types/log-poster.js';

/**
 * Background pipe-client that drains worker {@link LogEntry} values
 * from a bounded queue and writes them as NDJSON over a named pipe to
 * the extension-side LogServer. When the pipe is unavailable (no
 * `--service log=<address>` argument, the connect attempt fails, or the pipe
 * subsequently breaks) the client transparently falls back to writing
 * each record to stderr in a human-readable single-line format.
 *
 * Composed over {@link PipeStreamingClient}. This type owns the wire
 * shape (NDJSON entry + greeting) and the stderr fallback format; the
 * streaming primitive owns connect, queue, drain, and disposal.
 *
 * TypeScript counterpart of `LoggingClient` in
 * `AutoContext.Framework`.
 */
export class LoggingClient implements LogPoster {
    private readonly stream: PipeStreamingClient<LogEntry>;

    constructor(pipeName: string, clientName: string) {
        if (clientName.trim() === '') {
            throw new Error('clientName must be a non-empty string.');
        }

        const transport = new PipeTransport(NullLogger.instance);
        this.stream = new PipeStreamingClient<LogEntry>({
            transport,
            pipeName,
            serialize: LoggingClient.serializeEntry,
            logger: NullLogger.instance,
            greeting: LoggingClient.serializeGreeting(clientName),
            fallback: LoggingClient.writeStderr,
        });
    }

    post(entry: LogEntry): void {
        this.stream.post(entry);
    }

    dispose(): Promise<void> {
        return this.stream.dispose();
    }

    private static serializeGreeting(clientName: string): Buffer {
        const greeting: JsonLogGreeting = { clientName };
        return Buffer.from(JSON.stringify(greeting) + '\n', 'utf8');
    }

    private static serializeEntry(entry: LogEntry): Buffer {
        const wire: JsonLogEntry = {
            category: entry.category,
            level: entry.level,
            message: entry.message,
            ...(entry.exception !== undefined ? { exception: entry.exception } : {}),
            ...(entry.correlationId !== undefined ? { correlationId: entry.correlationId } : {}),
        };
        return Buffer.from(JSON.stringify(wire) + '\n', 'utf8');
    }

    private static writeStderr(entry: LogEntry): void {
        try {
            const prefix = entry.correlationId === undefined ? '' : `[${entry.correlationId}] `;
            process.stderr.write(`${prefix}${entry.level}: ${entry.category}: ${entry.message}\n`);
            if (entry.exception !== undefined) {
                process.stderr.write(`${entry.exception}\n`);
            }
        }
        catch {
            // stderr is gone — nothing we can do.
        }
    }
}
