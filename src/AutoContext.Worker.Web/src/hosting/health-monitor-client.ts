import { connect, type Socket } from 'node:net';
import type { Logger } from '#types/logger.js';

/**
 * Background pipe-client that announces this worker's identity to the
 * extension-side `HealthMonitorServer` and keeps the connection open
 * for the lifetime of the worker process. The server treats the live
 * socket as a liveness signal: when the worker exits the OS closes
 * the socket and the extension UI updates its "running" state.
 *
 * When `pipeName` is empty (standalone runs, no extension parent) the
 * client is a no-op — workers stay diagnosable without the call site
 * needing to special case standalone scenarios.
 *
 * The wire protocol is intentionally minimal: connect, write the
 * worker id as UTF-8 (no length prefix, no greeting wrapper), keep
 * the socket open. Failures (broken pipe, no listener) are swallowed
 * — health reporting is best-effort and must never crash the worker.
 *
 * TypeScript counterpart of `HealthMonitorClient` in
 * `AutoContext.Worker.Shared`.
 */
export class HealthMonitorClient {
    private static readonly CONNECT_TIMEOUT_MS = 2000;

    private readonly pipeName: string;
    private readonly workerId: string;
    private readonly logger: Logger | undefined;
    private socket: Socket | undefined;
    private startPromise: Promise<void> | undefined;
    private disposed = false;

    constructor(pipeName: string, workerId: string, logger?: Logger) {
        if (workerId.trim() === '') {
            throw new Error('workerId must be a non-empty string.');
        }
        this.pipeName = pipeName;
        this.workerId = workerId;
        this.logger = logger;
    }

    /**
     * Connects to the health-monitor pipe and writes the worker id.
     * Resolves once the id has been flushed (or immediately when no
     * pipe was supplied). The socket is intentionally kept open
     * afterwards. Safe to call multiple times — concurrent callers
     * share a single completion promise.
     */
    async start(): Promise<void> {
        if (this.startPromise !== undefined) {
            return this.startPromise;
        }
        this.startPromise = this.startCore();
        return this.startPromise;
    }

    /**
     * Closes the socket so the extension observes the disconnect and
     * marks the worker as no longer running. Safe to call multiple
     * times.
     */
    async dispose(): Promise<void> {
        if (this.disposed) {
            return;
        }
        this.disposed = true;
        const socket = this.socket;
        this.socket = undefined;
        if (socket !== undefined) {
            await new Promise<void>((resolve) => {
                socket.once('close', () => resolve());
                socket.destroy();
            });
        }
    }

    private async startCore(): Promise<void> {
        if (this.pipeName === '') {
            this.logger?.debug('Health-monitor pipe not configured; skipping liveness signal.');
            return;
        }

        const pipePath = HealthMonitorClient.normalizePipePath(this.pipeName);

        try {
            const socket = await this.connectWithTimeout(pipePath);

            if (this.disposed) {
                socket.destroy();
                return;
            }

            await new Promise<void>((resolve, reject) => {
                socket.write(this.workerId, 'utf8', (err) => {
                    if (err !== undefined && err !== null) {
                        reject(err);
                        return;
                    }
                    resolve();
                });
            });

            // Swallow late socket errors — health reporting is best-effort.
            socket.on('error', () => { /* see class doc */ });

            this.socket = socket;
            this.logger?.debug(
                `Connected to health-monitor pipe '${this.pipeName}' as worker '${this.workerId}'.`);
        }
        catch (err) {
            const message = err instanceof Error ? err.message : String(err);
            this.logger?.warn(
                `Failed to connect to health-monitor pipe '${this.pipeName}' as worker '${this.workerId}': ${message}`);
        }
    }

    private connectWithTimeout(pipePath: string): Promise<Socket> {
        return new Promise<Socket>((resolve, reject) => {
            const socket = connect(pipePath);

            const timer = setTimeout(() => {
                socket.destroy();
                reject(new Error(`Timed out connecting to health-monitor pipe '${pipePath}' after ${HealthMonitorClient.CONNECT_TIMEOUT_MS}ms.`));
            }, HealthMonitorClient.CONNECT_TIMEOUT_MS);
            timer.unref();

            socket.once('connect', () => {
                clearTimeout(timer);
                resolve(socket);
            });
            socket.once('error', (err) => {
                clearTimeout(timer);
                reject(err);
            });
        });
    }

    private static normalizePipePath(name: string): string {
        if (name.startsWith('\\\\') || name.startsWith('/')) {
            return name;
        }
        return process.platform === 'win32'
            ? `\\\\.\\pipe\\${name}`
            : `/tmp/CoreFxPipe_${name}`;
    }
}
