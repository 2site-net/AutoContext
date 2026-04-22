import * as net from 'node:net';
import * as fs from 'node:fs';
import type { Socket } from 'node:net';
import { readMessage, writeMessage } from './pipe-framing.js';
import type { McpTask } from './mcp-task.js';
import type { WorkerHostOptions } from './worker-host-options.js';

/**
 * Named-pipe server that accepts per-task connections and dispatches
 * each one to an {@link McpTask} instance. TypeScript counterpart of
 * the C# `McpToolService` in `AutoContext.Worker.Shared`.
 *
 * Wire protocol (per-task, see `docs/architecture.md` § "Protocol &
 * Contracts"): 4-byte little-endian length prefix + UTF-8 JSON payload.
 *
 * Request:  `{ "mcpTask", "data", "editorconfig" }`
 * Response: `{ "mcpTask", "status", "output", "error" }`
 *
 * Any `editorconfig` object on the request envelope is flattened into
 * `data` as properties prefixed with `editorconfig.` before the task
 * is invoked, so tasks see a single payload.
 *
 * One connection = one task call. The service spawns a per-connection
 * handler and immediately returns to accepting the next connection.
 */
export class McpToolService {
    private readonly options: WorkerHostOptions;
    private readonly tasks: ReadonlyMap<string, McpTask>;
    private server: net.Server | undefined;
    private readonly inFlight = new Set<Promise<void>>();
    private readonly sockets = new Set<Socket>();
    private readonly stopController = new AbortController();

    constructor(options: WorkerHostOptions, tasks: readonly McpTask[]) {
        if (options.pipe.trim() === '') {
            throw new Error('Missing required configuration: --pipe');
        }
        if (options.readyMarker.trim() === '') {
            throw new Error('Missing required configuration: readyMarker');
        }

        const map = new Map<string, McpTask>();
        for (const task of tasks) {
            if (map.has(task.taskName)) {
                throw new Error(
                    `Duplicate McpTask registration for task name '${task.taskName}'.`,
                );
            }
            map.set(task.taskName, task);
        }

        this.options = options;
        this.tasks = map;
    }

    /**
     * Starts the pipe server. Resolves once the listener is active and
     * the ready marker has been written to stderr. The returned promise
     * only rejects on immediate bind failure; subsequent per-connection
     * failures are handled in-band as error envelopes.
     *
     * When {@link signal} is aborted the server stops accepting new
     * connections, in-flight handlers are awaited, and the method
     * resolves cleanly.
     */
    async start(signal: AbortSignal): Promise<void> {
        if (this.server !== undefined) {
            throw new Error('McpToolService has already been started.');
        }

        const server = net.createServer((socket) => {
            this.sockets.add(socket);
            socket.once('close', () => this.sockets.delete(socket));
            const handler = this.handleConnection(socket, signal);
            this.inFlight.add(handler);
            void handler.finally(() => this.inFlight.delete(handler));
        });

        this.server = server;

        const pipePath = normalizePipePath(this.options.pipe);
        await listenWithStaleRecovery(server, pipePath);
        process.stderr.write(this.options.readyMarker + '\n');

        if (signal.aborted) {
            await this.stop();
            return;
        }

        signal.addEventListener('abort', () => void this.stop(), { once: true });
    }

    /**
     * Stops accepting new connections and awaits any in-flight
     * handlers. Safe to call multiple times.
     */
    async stop(): Promise<void> {
        const server = this.server;
        if (server === undefined) {
            return;
        }
        this.server = undefined;

        // Abort any mid-flight reads/writes so handlers unwind instead
        // of blocking server.close() indefinitely.
        if (!this.stopController.signal.aborted) {
            this.stopController.abort();
        }

        // Force-destroy live sockets so pending I/O rejects and the
        // server's internal handle count drops to zero.
        for (const socket of this.sockets) {
            socket.destroy();
        }
        this.sockets.clear();

        await new Promise<void>((resolve) => {
            server.close(() => resolve());
        });

        await Promise.allSettled(this.inFlight);
    }

    private async handleConnection(socket: Socket, signal: AbortSignal): Promise<void> {
        // `AbortSignal.any` gives us a signal that fires on either source.
        // Crucially, its internal listeners are disposed when the combined
        // signal is garbage-collected, so per-connection linking does not
        // leak listeners on long-lived signals.
        const linkedSignal = AbortSignal.any([signal, this.stopController.signal]);
        try {
            const requestBytes = await readMessage(socket, linkedSignal);
            if (requestBytes === null) {
                return;
            }

            const responseBytes = await this.dispatch(requestBytes, linkedSignal);
            await writeMessage(socket, responseBytes, linkedSignal);
        } catch {
            // Client disconnect, abort, or parse failure — the wire is already
            // broken; nothing more to report back. Matches C# behavior
            // (swallow IOException/ObjectDisposedException at this boundary).
        } finally {
            socket.end();
            socket.destroy();
        }
    }

    private async dispatch(requestBytes: Buffer, signal: AbortSignal): Promise<Buffer> {
        let taskName = '';

        try {
            const request: unknown = JSON.parse(requestBytes.toString('utf8'));
            if (!isObject(request)) {
                return buildErrorResponse('', 'Request must be a JSON object.');
            }

            const rawTaskName = request['mcpTask'];
            if (typeof rawTaskName !== 'string' || rawTaskName.length === 0) {
                return buildErrorResponse('', "Request is missing required field 'mcpTask'.");
            }
            taskName = rawTaskName;

            const task = this.tasks.get(taskName);
            if (task === undefined) {
                return buildErrorResponse(taskName, `Unknown task '${taskName}'.`);
            }

            const data = buildTaskData(request);
            const output = await task.execute(data, signal);
            return buildSuccessResponse(taskName, output);
        } catch (ex) {
            if (ex instanceof SyntaxError) {
                return buildErrorResponse(taskName, `Malformed request JSON: ${ex.message}`);
            }
            const { name, message } = describeError(ex);
            return buildErrorResponse(taskName, `Task threw ${name}: ${message}`);
        }
    }
}

function describeError(ex: unknown): { name: string; message: string } {
    if (ex instanceof Error) {
        return { name: ex.name || 'Error', message: ex.message || String(ex) };
    }
    return { name: 'Error', message: ex === null ? 'null' : String(ex) };
}

async function listenWithStaleRecovery(server: net.Server, pipePath: string): Promise<void> {
    try {
        await listenOnce(server, pipePath);
        return;
    } catch (ex) {
        const err = ex as NodeJS.ErrnoException;
        // On Unix, a leftover socket file from a prior crash causes
        // EADDRINUSE. Probe it by attempting to connect; if nothing is
        // listening, unlink the stale inode and retry once.
        if (
            process.platform === 'win32' ||
            err.code !== 'EADDRINUSE' ||
            !pipePath.startsWith('/')
        ) {
            throw ex;
        }
        const isStale = await probeStaleSocket(pipePath);
        if (!isStale) {
            throw ex;
        }
        try {
            fs.unlinkSync(pipePath);
        } catch {
            throw ex;
        }
        await listenOnce(server, pipePath);
    }
}

function listenOnce(server: net.Server, pipePath: string): Promise<void> {
    return new Promise<void>((resolve, reject) => {
        const onError = (err: Error): void => {
            server.removeListener('listening', onListening);
            reject(err);
        };
        const onListening = (): void => {
            server.removeListener('error', onError);
            resolve();
        };
        server.once('error', onError);
        server.once('listening', onListening);
        server.listen(pipePath);
    });
}

function probeStaleSocket(pipePath: string): Promise<boolean> {
    return new Promise<boolean>((resolve) => {
        const socket = net.connect(pipePath);
        const finish = (stale: boolean): void => {
            socket.removeAllListeners();
            socket.destroy();
            resolve(stale);
        };
        socket.once('connect', () => finish(false));
        socket.once('error', (err: NodeJS.ErrnoException) => {
            finish(err.code === 'ECONNREFUSED' || err.code === 'ENOENT');
        });
    });
}

function normalizePipePath(pipe: string): string {
    if (process.platform !== 'win32') {
        return pipe;
    }
    if (pipe.startsWith('\\\\.\\pipe\\') || pipe.startsWith('\\\\?\\pipe\\')) {
        return pipe;
    }
    return `\\\\.\\pipe\\${pipe}`;
}

function buildTaskData(request: Record<string, unknown>): Record<string, unknown> {
    const data = isObject(request['data']) ? { ...request['data'] } : {};
    const ec = request['editorconfig'];

    if (!isObject(ec)) {
        return data;
    }

    for (const [key, value] of Object.entries(ec)) {
        if (typeof value === 'string') {
            data[`editorconfig.${key}`] = value;
        }
    }

    return data;
}

function buildSuccessResponse(taskName: string, output: unknown): Buffer {
    return Buffer.from(
        JSON.stringify({
            mcpTask: taskName,
            status: 'ok',
            output: output ?? null,
            error: '',
        }),
        'utf8',
    );
}

function buildErrorResponse(taskName: string, error: string): Buffer {
    return Buffer.from(
        JSON.stringify({
            mcpTask: taskName,
            status: 'error',
            output: null,
            error,
        }),
        'utf8',
    );
}

function isObject(value: unknown): value is Record<string, unknown> {
    return typeof value === 'object' && value !== null && !Array.isArray(value);
}
