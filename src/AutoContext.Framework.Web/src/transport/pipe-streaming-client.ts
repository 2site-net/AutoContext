import type { Socket } from 'node:net';

import type { LoggerFacade } from '../logging/logger-facade.js';
import { PipeTransport } from './pipe-transport.js';

/**
 * Layer-3 streaming client: drains items from a bounded queue and
 * writes their serialized bytes over a named pipe. Drop-oldest queue
 * semantics keep callers non-blocking. On any I/O failure the stream
 * is closed and remaining items (and any future {@link post} items)
 * are routed to {@link PipeStreamingClientOptions.fallback}.
 *
 * No reconnect policy is built in — matches today's "logger of last
 * resort" behavior in `LoggingClient`. Designed to be wrapped by an
 * endpoint class that supplies the {@link T} type, the serializer,
 * and the fallback path.
 *
 * Counterpart of the C# `PipeStreamingClient<T>` in
 * `AutoContext.Framework`.
 */
export interface PipeStreamingClientOptions<T> {
    readonly transport: PipeTransport;
    readonly pipeName: string;
    readonly serialize: (item: T) => Buffer | Uint8Array;
    readonly logger: LoggerFacade;
    readonly greeting?: Buffer | Uint8Array;
    readonly fallback?: (item: T) => void;
    readonly queueCapacity?: number;
    readonly connectTimeoutMs?: number;
}

export class PipeStreamingClient<T> {
    private static readonly DEFAULT_QUEUE_CAPACITY = 1024;
    private static readonly DEFAULT_CONNECT_TIMEOUT_MS = 2000;
    private static readonly DEFAULT_DRAIN_TIMEOUT_MS = 2000;

    private readonly transport: PipeTransport;
    private readonly pipeName: string;
    private readonly serialize: (item: T) => Buffer | Uint8Array;
    private readonly fallback: ((item: T) => void) | undefined;
    private readonly greeting: Buffer | Uint8Array | undefined;
    private readonly logger: LoggerFacade;
    private readonly queueCapacity: number;
    private readonly connectTimeoutMs: number;
    private readonly queue: T[] = [];
    private readonly waiters: Array<() => void> = [];
    private completed = false;
    private socket: Socket | undefined;
    private readonly drainTask: Promise<void>;
    private disposeTask: Promise<void> | undefined;

    constructor(opts: PipeStreamingClientOptions<T>) {
        this.transport = opts.transport;
        this.pipeName = opts.pipeName;
        this.serialize = opts.serialize;
        this.fallback = opts.fallback;
        this.greeting = opts.greeting;
        this.logger = opts.logger;
        this.queueCapacity = opts.queueCapacity ?? PipeStreamingClient.DEFAULT_QUEUE_CAPACITY;
        this.connectTimeoutMs = opts.connectTimeoutMs ?? PipeStreamingClient.DEFAULT_CONNECT_TIMEOUT_MS;
        this.drainTask = this.drain();
    }

    /**
     * Enqueues `item` for off-thread delivery. Never blocks; if the
     * queue is full the oldest entry is dropped.
     */
    post(item: T): void {
        if (this.completed) {
            return;
        }
        if (this.queue.length >= this.queueCapacity) {
            this.queue.shift();
        }
        this.queue.push(item);
        const waiter = this.waiters.shift();
        if (waiter !== undefined) {
            waiter();
        }
    }

    /**
     * Stops accepting items, waits up to two seconds for the drain
     * loop to flush, then tears the socket down. Safe to call multiple
     * times — concurrent callers share a single completion promise.
     */
    async dispose(): Promise<void> {
        this.disposeTask ??= this.disposeCore();
        return this.disposeTask;
    }

    private async disposeCore(): Promise<void> {
        this.completed = true;
        const waiters = this.waiters.splice(0);
        for (const waiter of waiters) {
            waiter();
        }

        let timer: NodeJS.Timeout | undefined;
        const timeout = new Promise<void>((resolve) => {
            timer = setTimeout(resolve, PipeStreamingClient.DEFAULT_DRAIN_TIMEOUT_MS);
            timer.unref();
        });
        try {
            await Promise.race([this.drainTask, timeout]);
        }
        finally {
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

        if (socket !== null) {
            // Swallow late socket errors — without this the process crashes
            // on EPIPE / peer close because `PipeTransport.connect` removes
            // its own error listener once the connection is established.
            socket.on('error', () => { /* see comment */ });

            if (this.greeting !== undefined && this.greeting.length > 0
                && !await PipeStreamingClient.tryWrite(socket, this.greeting)) {
                socket.destroy();
                socket = null;
            }
        }
        this.socket = socket ?? undefined;

        try {
            while (true) {
                const item = await this.dequeue();
                if (item === undefined) {
                    return;
                }
                if (this.socket !== undefined) {
                    const bytes = this.serialize(item);
                    if (await PipeStreamingClient.tryWrite(this.socket, bytes)) {
                        continue;
                    }
                    this.logger.debug(`Pipe stream to '${this.pipeName}' broken; routing further items to fallback.`);
                    this.socket.destroy();
                    this.socket = undefined;
                }
                if (this.fallback !== undefined) {
                    this.fallback(item);
                }
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

    private dequeue(): Promise<T | undefined> {
        if (this.queue.length > 0) {
            return Promise.resolve(this.queue.shift());
        }
        if (this.completed) {
            return Promise.resolve(undefined);
        }
        return new Promise<T | undefined>((resolve) => {
            this.waiters.push(() => {
                if (this.queue.length > 0) {
                    resolve(this.queue.shift());
                    return;
                }
                resolve(undefined);
            });
        });
    }

    private async tryConnect(): Promise<Socket | null> {
        if (this.pipeName === '') {
            return null;
        }
        const controller = new AbortController();
        const timer = setTimeout(() => controller.abort(), this.connectTimeoutMs);
        timer.unref();
        try {
            return await this.transport.connect(this.pipeName, controller.signal);
        }
        catch {
            return null;
        }
        finally {
            clearTimeout(timer);
        }
    }

    private static tryWrite(socket: Socket, bytes: Buffer | Uint8Array): Promise<boolean> {
        return new Promise<boolean>((resolve) => {
            if (socket.destroyed || !socket.writable) {
                resolve(false);
                return;
            }
            socket.write(bytes, (err) => {
                resolve(err === null || err === undefined);
            });
        });
    }
}
