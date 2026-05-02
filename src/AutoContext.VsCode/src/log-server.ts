import * as vscode from 'vscode';
import { type Socket } from 'node:net';
import { createInterface } from 'node:readline';
import { BoundPipeListener, PipeListener } from 'autocontext-framework-web';
import { IdentifierFactory } from './identifier-factory.js';
import { ServerEntry } from './server-entry.js';
import type { ChannelLogger } from 'autocontext-framework-web';

/**
 * Wire shape of the greeting line every worker emits as the first
 * NDJSON line on the pipe. The {@link clientName} is the worker's
 * `IHostEnvironment.ApplicationName` (e.g. `AutoContext.Worker.DotNet`)
 * — used to route subsequent records to a per-worker output channel.
 */
interface JsonLogGreeting {
    readonly clientName: string;
}

/**
 * Wire shape of one NDJSON log record. `level` is the .NET `LogLevel`
 * enum name (`Trace`, `Debug`, `Information`, `Warning`, `Error`,
 * `Critical`). `correlationId` is the per-`tools/call` short id minted
 * by the MCP server and threaded through every `TaskRequest` — absent
 * for log records emitted outside of a task dispatch (e.g. worker
 * startup).
 */
interface JsonLogEntry {
    readonly category: string;
    readonly level: string;
    readonly message: string;
    readonly exception?: string;
    readonly correlationId?: string;
}

/**
 * Receives structured log records from every spawned `AutoContext.Worker.*`
 * process over a single named pipe (NDJSON, line-delimited JSON), and
 * fans them out to per-worker `LogOutputChannel`s under the AutoContext
 * Output panel. Mirrors the topology of {@link HealthMonitorServer}:
 * one server, many client connections.
 *
 * Wire format (per-line, UTF-8, terminated with `\n`):
 *
 *  - Greeting (first line from a connection):
 *    `{ "clientName": "AutoContext.Worker.DotNet" }`
 *  - Log record (every subsequent line):
 *    `{ "category": "<ILogger category>", "level": "<LogLevel name>",
 *       "message": "<rendered message>", "exception": "<.ToString() text>" }`
 *
 * If the pipe is unavailable or drops, workers fall back to writing
 * each record to their own stderr — those lines are still surfaced by
 * `WorkerManager`'s stderr line-handler, just in the WorkerManager
 * subcategory rather than a per-worker channel. So a missing LogServer
 * degrades gracefully.
 */
export class LogServer implements vscode.Disposable {
    private readonly pipeName: string;
    private readonly stopController = new AbortController();
    private bound: BoundPipeListener | undefined;

    constructor(
        private readonly logger: ChannelLogger,
        instanceId: string,
    ) {
        this.pipeName = IdentifierFactory.createServiceAddress('log', instanceId);
    }

    /** Pipe name workers must connect to. */
    getPipeName(): string {
        return this.pipeName;
    }

    /** Starts the named-pipe accept loop. Idempotent. */
    async start(): Promise<void> {
        if (this.bound !== undefined) {
            return;
        }

        const listener = new PipeListener(this.pipeName, this.logger);
        this.bound = await listener.bind();
        this.logger.info(`Listening on pipe: ${this.pipeName}`);

        void this.bound.run(
            (socket, signal) => this.handleConnection(socket, signal),
            this.stopController.signal,
        );
    }

    private handleConnection(socket: Socket, signal: AbortSignal): Promise<void> {
        // Per-connection state — captured by the `line` handler.
        let workerName: string | undefined;
        let perWorkerLogger: ChannelLogger | undefined;

        const reader = createInterface({ input: socket });

        reader.on('line', (line: string) => {
            // Trim only trailing whitespace (e.g. CR on Windows-built
            // payloads), preserve embedded structure.
            const trimmed = line.trimEnd();
            if (trimmed.length === 0) {
                return;
            }

            let payload: unknown;
            try {
                payload = JSON.parse(trimmed);
            }
            catch (err) {
                this.logger.warn(`Dropping malformed log line from ${workerName ?? 'unknown worker'}: ${trimmed}`, err);
                return;
            }

            if (!workerName) {
                if (!LogServer.isGreeting(payload)) {
                    this.logger.warn(`Expected greeting line, got: ${trimmed}`);
                    return;
                }
                workerName = payload.clientName;
                perWorkerLogger = this.logger.forChannel(`AutoContext: ${ServerEntry.stripNamePrefix(workerName)}`);
                return;
            }

            if (!LogServer.isLogEntry(payload)) {
                this.logger.warn(`Dropping non-record line from ${workerName}: ${trimmed}`);
                return;
            }

            const method = LogServer.levelToMethod(payload.level);
            if (!method || !perWorkerLogger) {
                return;
            }
            const message = payload.correlationId
                ? `[${payload.correlationId}] ${payload.message}`
                : payload.message;
            perWorkerLogger.forCategory(payload.category)[method](message, payload.exception);
        });

        return new Promise<void>((resolve) => {
            const finish = (): void => {
                signal.removeEventListener('abort', finish);
                reader.close();
                resolve();
            };
            socket.once('close', finish);
            socket.once('error', finish);
            signal.addEventListener('abort', finish, { once: true });
        });
    }

    dispose(): void {
        if (!this.stopController.signal.aborted) {
            this.stopController.abort();
        }
        // Fire-and-forget OS resource cleanup; vscode.Disposable is sync.
        if (this.bound !== undefined) {
            void this.bound.dispose();
            this.bound = undefined;
        }
    }

    /**
     * Type-guard: a parsed payload is a greeting iff it carries the
     * `clientName` field.
     */
    private static isGreeting(value: unknown): value is JsonLogGreeting {
        return (
            typeof value === 'object' &&
            value !== null &&
            typeof (value as { clientName?: unknown }).clientName === 'string'
        );
    }

    /**
     * Type-guard: a parsed payload is a log entry iff it carries the
     * three required string fields.
     */
    private static isLogEntry(value: unknown): value is JsonLogEntry {
        if (typeof value !== 'object' || value === null) {
            return false;
        }
        const v = value as { category?: unknown; level?: unknown; message?: unknown };
        return typeof v.category === 'string' && typeof v.level === 'string' && typeof v.message === 'string';
    }

    /**
     * Maps a .NET `LogLevel` string to a {@link ChannelLogger} method name.
     * `Critical` and `None` both fall through to `error` (there is no
     * higher level on our facade).
     */
    private static levelToMethod(level: string): 'trace' | 'debug' | 'info' | 'warn' | 'error' | undefined {
        switch (level) {
            case 'Trace': return 'trace';
            case 'Debug': return 'debug';
            case 'Information': return 'info';
            case 'Warning': return 'warn';
            case 'Error':
            case 'Critical':
                return 'error';
            case 'None': return undefined;
            default: return 'info';
        }
    }
}
