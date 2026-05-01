import type { Socket } from 'node:net';

import type { Logger } from '../logging/logger.js';
import { PipeTransport } from './pipe-transport.js';

/**
 * Layer-3 keep-alive client: connects, writes a tiny handshake once,
 * and holds the socket open as a process liveness signal. The remote
 * peer treats the live socket as "this host is running" and observes
 * the close when the host exits.
 *
 * When the pipe name is empty (standalone runs, no parent listening)
 * {@link start} is a no-op so call sites don't need to special-case
 * standalone scenarios. Connection failures are best-effort — they're
 * logged at warning and swallowed.
 *
 * Counterpart of the C# `PipeKeepAliveClient` in
 * `AutoContext.Framework`.
 */
export class PipeKeepAliveClient {
    private static readonly DEFAULT_CONNECT_TIMEOUT_MS = 2000;

    private readonly transport: PipeTransport;
    private readonly logger: Logger;
    private socket: Socket | undefined;
    private startPromise: Promise<void> | undefined;
    private disposed = false;

    constructor(transport: PipeTransport, logger: Logger) {
        this.transport = transport;
        this.logger = logger;
    }

    /**
     * Connects to {@link pipeName} and writes {@link handshake} as a
     * single best-effort attempt. On success the socket is held until
     * {@link dispose}. Safe to call multiple times — concurrent callers
     * share a single completion promise.
     */
    start(
        pipeName: string,
        handshake: Buffer | Uint8Array,
        connectTimeoutMs: number = PipeKeepAliveClient.DEFAULT_CONNECT_TIMEOUT_MS,
    ): Promise<void> {
        if (this.startPromise !== undefined) {
            return this.startPromise;
        }
        this.startPromise = this.startCore(pipeName, handshake, connectTimeoutMs);
        return this.startPromise;
    }

    /**
     * Closes the held socket so the remote peer observes the
     * disconnect. Safe to call multiple times.
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

    private async startCore(
        pipeName: string,
        handshake: Buffer | Uint8Array,
        connectTimeoutMs: number,
    ): Promise<void> {
        if (pipeName === '') {
            this.logger.debug('Keep-alive pipe not configured; skipping connection.');
            return;
        }

        const controller = new AbortController();
        const timer = setTimeout(() => controller.abort(), connectTimeoutMs);
        timer.unref();

        try {
            const socket = await this.transport.connect(pipeName, controller.signal);

            if (this.disposed) {
                socket.destroy();
                return;
            }

            if (handshake.length > 0) {
                await new Promise<void>((resolve, reject) => {
                    socket.write(handshake, (err) => {
                        if (err !== null && err !== undefined) {
                            reject(err);
                            return;
                        }
                        resolve();
                    });
                });
            }

            // Swallow late socket errors — keep-alive is best-effort.
            socket.on('error', () => { /* see class doc */ });

            this.socket = socket;
        }
        catch (err) {
            const message = err instanceof Error ? err.message : String(err);
            this.logger.warn(`Failed to connect keep-alive pipe '${pipeName}': ${message}`);
        }
        finally {
            clearTimeout(timer);
        }
    }
}
