import * as net from 'node:net';
import * as fs from 'node:fs';
import type { Server, Socket } from 'node:net';
import { LengthPrefixedFrameCodec } from 'autocontext-framework-web';
import type { McpTask } from '#types/mcp-task.js';
import type { WorkerHostOptions } from '#types/worker-host-options.js';
import { CorrelationScope } from '../logging/correlation-scope.js';
import type { Logger } from '#types/logger.js';

/**
 * The worker's task runner. Listens on a named pipe for task requests
 * sent by the MCP server, looks the task up by name in the worker's
 * registered {@link McpTask} set, executes it, and sends the result
 * back on the same connection. TypeScript counterpart of the C#
 * `McpTaskDispatcherService` in `AutoContext.Worker.Shared`.
 *
 * Each pipe connection carries exactly one task call: read one request
 * envelope, run one task, write one response envelope, close. The MCP
 * server is responsible for fanning a multi-task tool invocation out
 * across multiple concurrent connections and aggregating the results;
 * the worker only sees individual task calls.
 *
 * Wire protocol (see `docs/architecture.md` § "Protocol & Contracts"):
 * 4-byte little-endian length prefix + UTF-8 JSON payload.
 *
 * Request:  `{ "mcpTask", "data", "editorconfig" }`
 * Response: `{ "mcpTask", "status", "output", "error" }`
 *
 * Any `editorconfig` object on the request envelope is flattened into
 * `data` as properties prefixed with `editorconfig.` before the task
 * is invoked, so tasks see a single payload.
 */
export class WorkerTaskDispatcherService {
    private readonly options: WorkerHostOptions;
    private readonly tasks: ReadonlyMap<string, McpTask>;
    private readonly logger: Logger;
    private server: net.Server | undefined;
    private readonly inFlight = new Set<Promise<void>>();
    private readonly sockets = new Set<Socket>();
    private readonly stopController = new AbortController();
    private stopPromise: Promise<void> | undefined;

    constructor(options: WorkerHostOptions, tasks: readonly McpTask[], logger: Logger) {
        if (options.pipe.trim() === '') {
            throw new Error('Missing required configuration: pipe');
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
        this.logger = logger;
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
            throw new Error('WorkerTaskDispatcherService has already been started.');
        }

        const server = net.createServer((socket) => {
            this.sockets.add(socket);
            socket.once('close', () => this.sockets.delete(socket));
            const handler = this.handleConnection(socket, signal);
            this.inFlight.add(handler);
            void handler.finally(() => this.inFlight.delete(handler));
        });

        this.server = server;

        const pipePath = WorkerTaskDispatcherService.normalizePipePath(this.options.pipe);
        this.logger.info(`Worker listening on pipe: ${pipePath}`);
        await WorkerTaskDispatcherService.listenWithStaleRecovery(server, pipePath);
        process.stderr.write(this.options.readyMarker + '\n');
        this.logger.info(`Worker ready marker emitted on pipe: ${pipePath}`);

        if (signal.aborted) {
            await this.stop();
            return;
        }

        signal.addEventListener('abort', () => void this.stop(), { once: true });
    }

    /**
     * Stops accepting new connections and awaits any in-flight
     * handlers. Safe to call multiple times; concurrent callers share
     * the same completion promise.
     */
    async stop(): Promise<void> {
        if (this.stopPromise !== undefined) {
            return this.stopPromise;
        }
        const server = this.server;
        if (server === undefined) {
            return;
        }
        this.server = undefined;
        this.stopPromise = this.stopCore(server);
        return this.stopPromise;
    }

    private async stopCore(server: Server): Promise<void> {
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
            const channel = new LengthPrefixedFrameCodec(socket);
            const requestBytes = await channel.read(linkedSignal);
            if (requestBytes === null) {
                return;
            }

            const responseBytes = await this.dispatch(requestBytes, linkedSignal);
            await channel.write(responseBytes, linkedSignal);
        } catch (ex) {
            // `dispatch()` converts task/parse failures into structured
            // error envelopes, so the only errors that reach this catch
            // are channel-level: peer disconnect, signal abort, or the
            // pipe being torn down mid-transfer (all expected), or
            // protocol corruption / unexpected stream failures (real
            // bugs). Surface the latter through the logger (and stderr
            // as a last-resort backstop) instead of swallowing them.
            // Matches C# behavior of only tolerating
            // IOException/ObjectDisposedException at this boundary.
            if (!WorkerTaskDispatcherService.isExpectedConnectionError(ex)) {
                const { name, message } = WorkerTaskDispatcherService.describeError(ex);
                this.logger.error(`Unexpected error in connection handler: ${name}: ${message}`, ex);
            }
        } finally {
            socket.end();
            socket.destroy();
        }
    }

    private async dispatch(requestBytes: Buffer, signal: AbortSignal): Promise<Buffer> {
        // Parse the envelope first under its own narrow handler. A
        // malformed envelope has no correlation id available, so it
        // can never be scope-correlated — but neither does it execute
        // any user task, so there's nothing to mis-attribute.
        let request: unknown;
        try {
            request = JSON.parse(requestBytes.toString('utf8'));
        } catch (ex) {
            const message = ex instanceof Error ? ex.message : String(ex);
            return WorkerTaskDispatcherService.buildErrorResponse('', `Malformed request JSON: ${message}`);
        }

        if (!WorkerTaskDispatcherService.isObject(request)) {
            return WorkerTaskDispatcherService.buildErrorResponse('', 'Request must be a JSON object.');
        }

        const rawTaskName = request['mcpTask'];
        if (typeof rawTaskName !== 'string' || rawTaskName.length === 0) {
            return WorkerTaskDispatcherService.buildErrorResponse('', "Request is missing required field 'mcpTask'.");
        }
        const taskName = rawTaskName;
        const correlationId = WorkerTaskDispatcherService.readCorrelationId(request);

        // Run the dispatch — including the catch-all — inside the
        // CorrelationScope so every Logger call made by the
        // task and the failure handler is stamped with the same id
        // before reaching the LogServer pipe.
        const run = async (): Promise<Buffer> => {
            try {
                const task = this.tasks.get(taskName);
                if (task === undefined) {
                    return WorkerTaskDispatcherService.buildErrorResponse(taskName, `Unknown task '${taskName}'.`);
                }
                const data = WorkerTaskDispatcherService.buildTaskData(request);
                const output = await task.execute(data, signal);
                return WorkerTaskDispatcherService.buildSuccessResponse(taskName, output);
            } catch (ex) {
                const { name, message } = WorkerTaskDispatcherService.describeError(ex);
                this.logger.error(`Task '${taskName}' failed: ${name}: ${message}`, ex);
                return WorkerTaskDispatcherService.buildErrorResponse(taskName, `Task threw ${name}: ${message}`);
            }
        };

        return correlationId === undefined
            ? run()
            : CorrelationScope.run(correlationId, run);
    }

    private static readCorrelationId(request: Record<string, unknown>): string | undefined {
        const value = request['correlationId'];
        return typeof value === 'string' && value.length > 0 ? value : undefined;
    }

    private static describeError(ex: unknown): { name: string; message: string } {
        if (ex instanceof Error) {
            return { name: ex.name || 'Error', message: ex.message || String(ex) };
        }
        return { name: 'Error', message: ex === null ? 'null' : String(ex) };
    }

    /**
     * Returns true for the small set of errors that are an expected part
     * of normal pipe-server lifecycle: the abort signal firing, the peer
     * closing the connection, or the pipe being torn down mid-write.
     * Anything else is treated as a real bug and surfaced to stderr.
     */
    private static isExpectedConnectionError(ex: unknown): boolean {
        if (!(ex instanceof Error)) {
            return false;
        }
        if (ex.name === 'AbortError') {
            return true;
        }
        const code = (ex as NodeJS.ErrnoException).code;
        return code === 'ECONNRESET'
            || code === 'EPIPE'
            || code === 'ERR_STREAM_PREMATURE_CLOSE'
            || code === 'ERR_STREAM_DESTROYED';
    }

    private static async listenWithStaleRecovery(server: net.Server, pipePath: string): Promise<void> {
        try {
            await WorkerTaskDispatcherService.listenOnce(server, pipePath);
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
            const isStale = await WorkerTaskDispatcherService.probeStaleSocket(pipePath);
            if (!isStale) {
                throw ex;
            }
            try {
                fs.unlinkSync(pipePath);
            } catch {
                throw ex;
            }
            await WorkerTaskDispatcherService.listenOnce(server, pipePath);
        }
    }

    private static listenOnce(server: net.Server, pipePath: string): Promise<void> {
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

    private static probeStaleSocket(pipePath: string): Promise<boolean> {
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

    private static normalizePipePath(pipe: string): string {
        if (process.platform !== 'win32') {
            return pipe;
        }
        if (pipe.startsWith('\\\\.\\pipe\\') || pipe.startsWith('\\\\?\\pipe\\')) {
            return pipe;
        }
        return `\\\\.\\pipe\\${pipe}`;
    }

    private static buildTaskData(request: Record<string, unknown>): Record<string, unknown> {
        const data = WorkerTaskDispatcherService.isObject(request['data']) ? { ...request['data'] } : {};
        const ec = request['editorconfig'];

        if (!WorkerTaskDispatcherService.isObject(ec)) {
            return data;
        }

        for (const [key, value] of Object.entries(ec)) {
            if (typeof value === 'string') {
                data[`editorconfig.${key}`] = value;
            }
        }

        return data;
    }

    private static buildSuccessResponse(taskName: string, output: unknown): Buffer {
        // `JSON.stringify` throws on `BigInt`, circular references, and a
        // few other shapes. Without this guard those would propagate up to
        // the dispatcher's catch and be reported as `Task threw …`, which
        // is misleading — the task succeeded; only the response could not
        // be serialized. Synthesize a clear error envelope instead so the
        // client sees the real cause.
        let payload: string;
        try {
            payload = JSON.stringify({
                mcpTask: taskName,
                status: 'ok',
                output: output ?? null,
                error: '',
            });
        } catch (ex) {
            const { name, message } = WorkerTaskDispatcherService.describeError(ex);
            return WorkerTaskDispatcherService.buildErrorResponse(
                taskName,
                `Task output is not JSON-serializable: ${name}: ${message}`,
            );
        }
        return Buffer.from(payload, 'utf8');
    }

    private static buildErrorResponse(taskName: string, error: string): Buffer {
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

    private static isObject(value: unknown): value is Record<string, unknown> {
        return typeof value === 'object' && value !== null && !Array.isArray(value);
    }
}
