import { connect, type Socket } from 'node:net';
import type { LogGreetingWire, LogRecord, LogRecordWire } from '#types/log-record.js';
import type { LogSink } from '#types/log-sink.js';

/**
 * Background pipe-client that drains worker {@link LogRecord} values
 * from a bounded queue and writes them as NDJSON over a named pipe to
 * the extension-side LogServer. When the pipe is unavailable (no
 * `--log-pipe` argument, the connect attempt fails, or the pipe
 * subsequently breaks) the client transparently falls back to writing
 * each record to stderr in a human-readable single-line format.
 *
 * The bounded queue uses drop-oldest semantics — log spam can never
 * block worker code or grow memory unbounded. A single drain task
 * owns all I/O. Failures (broken pipe, serialisation errors) are
 * intentionally swallowed: this type IS the logger of last resort,
 * so it must never throw.
 *
 * TypeScript counterpart of `LogServerClient` in
 * `AutoContext.Worker.Shared`.
 */
export class LogServerClient implements LogSink {
    private static readonly QUEUE_CAPACITY = 1024;
    private static readonly CONNECT_TIMEOUT_MS = 2000;

    private readonly pipeName: string;
    private readonly clientName: string;
    private readonly queue: LogRecord[] = [];
    private readonly waiters: Array<() => void> = [];
    private completed = false;
    private socket: Socket | undefined;
    private readonly drainTask: Promise<void>;
    private disposeTask: Promise<void> | undefined;

    constructor(pipeName: string, clientName: string) {
        if (clientName.trim() === '') {
            throw new Error('clientName must be a non-empty string.');
        }
        this.pipeName = pipeName;
        this.clientName = clientName;
        this.drainTask = this.drain();
    }

    /**
     * Enqueues `record` for off-thread delivery. Never blocks; if the
     * queue is full the oldest record is dropped.
     */
    enqueue(record: LogRecord): void {
        if (this.completed) {
            return;
        }
        if (this.queue.length >= LogServerClient.QUEUE_CAPACITY) {
            this.queue.shift();
        }
        this.queue.push(record);
        const waiter = this.waiters.shift();
        if (waiter !== undefined) {
            waiter();
        }
    }

    /**
     * Stops accepting new records, waits up to two seconds for the
     * drain loop to flush, then tears the socket down. Safe to call
     * multiple times — concurrent callers share a single completion
     * promise.
     */
    async dispose(): Promise<void> {
        this.disposeTask ??= this.disposeCore();
        return this.disposeTask;
    }

    private async disposeCore(): Promise<void> {
        this.completed = true;
        // Wake any pending wait so the drain loop can observe completion.
        const waiters = this.waiters.splice(0);
        for (const waiter of waiters) {
            waiter();
        }

        let timer: NodeJS.Timeout | undefined;
        const timeout = new Promise<void>((resolve) => {
            timer = setTimeout(resolve, 2000);
            timer.unref();
        });
        try {
            await Promise.race([this.drainTask, timeout]);
        } finally {
            if (timer !== undefined) {
                clearTimeout(timer);
            }
        }

        const socket = this.socket;
        this.socket = undefined;
        if (socket !== undefined) {
            socket.destroy();
        }
    }

    private async drain(): Promise<void> {
        let socket = await this.tryConnect();
        if (socket !== null && !(await this.trySendGreeting(socket))) {
            socket.destroy();
            socket = null;
        }
        this.socket = socket ?? undefined;

        try {
            while (true) {
                const record = await this.dequeue();
                if (record === undefined) {
                    return;
                }
                if (this.socket !== undefined) {
                    if (await this.tryWritePipe(this.socket, record)) {
                        continue;
                    }
                    this.socket.destroy();
                    this.socket = undefined;
                }
                LogServerClient.writeStderr(record);
            }
        }
        finally {
            const s = this.socket;
            this.socket = undefined;
            if (s !== undefined) {
                s.destroy();
            }
        }
    }

    private dequeue(): Promise<LogRecord | undefined> {
        if (this.queue.length > 0) {
            return Promise.resolve(this.queue.shift());
        }
        if (this.completed) {
            return Promise.resolve(undefined);
        }
        return new Promise<LogRecord | undefined>((resolve) => {
            this.waiters.push(() => {
                if (this.queue.length > 0) {
                    resolve(this.queue.shift());
                    return;
                }
                resolve(undefined);
            });
        });
    }

    private tryConnect(): Promise<Socket | null> {
        if (this.pipeName.trim() === '') {
            return Promise.resolve(null);
        }
        const pipePath = process.platform === 'win32'
            ? `\\\\.\\pipe\\${this.pipeName}`
            : `/tmp/CoreFxPipe_${this.pipeName}`;

        return new Promise<Socket | null>((resolve) => {
            const sock = connect(pipePath);
            let settled = false;
            const settle = (value: Socket | null): void => {
                if (settled) {
                    return;
                }
                settled = true;
                clearTimeout(timer);
                sock.removeListener('connect', onConnect);
                sock.removeListener('error', onError);
                if (value === null) {
                    sock.destroy();
                }
                resolve(value);
            };
            const onConnect = (): void => settle(sock);
            const onError = (): void => settle(null);
            const timer = setTimeout(() => settle(null), LogServerClient.CONNECT_TIMEOUT_MS);
            timer.unref();
            sock.once('connect', onConnect);
            sock.once('error', onError);
        });
    }

    private trySendGreeting(socket: Socket): Promise<boolean> {
        const greeting: LogGreetingWire = { clientName: this.clientName };
        return LogServerClient.writeLine(socket, JSON.stringify(greeting));
    }

    private tryWritePipe(socket: Socket, record: LogRecord): Promise<boolean> {
        const wire: LogRecordWire = {
            category: record.category,
            level: record.level,
            message: record.message,
            ...(record.exception !== undefined ? { exception: record.exception } : {}),
            ...(record.correlationId !== undefined ? { correlationId: record.correlationId } : {}),
        };
        let json: string;
        try {
            json = JSON.stringify(wire);
        }
        catch {
            // Returning false routes the record through the stderr
            // fallback rather than silently losing it. The wire shape
            // contains only strings/undefined so this is essentially
            // unreachable today — the guard is here to keep the drain
            // loop honest if the type contract is ever violated.
            return Promise.resolve(false);
        }
        return LogServerClient.writeLine(socket, json);
    }

    private static writeLine(socket: Socket, json: string): Promise<boolean> {
        return new Promise<boolean>((resolve) => {
            if (socket.destroyed || !socket.writable) {
                resolve(false);
                return;
            }
            socket.write(json + '\n', (err) => {
                resolve(err === null || err === undefined);
            });
        });
    }

    private static writeStderr(record: LogRecord): void {
        try {
            const prefix = record.correlationId === undefined ? '' : `[${record.correlationId}] `;
            process.stderr.write(`${prefix}${record.level}: ${record.category}: ${record.message}\n`);
            if (record.exception !== undefined) {
                process.stderr.write(`${record.exception}\n`);
            }
        }
        catch {
            // stderr is gone — nothing we can do.
        }
    }
}
