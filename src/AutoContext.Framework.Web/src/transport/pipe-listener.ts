import { createServer, type Server, type Socket } from 'node:net';
import { unlinkSync } from 'node:fs';
import { connect } from 'node:net';
import { platform } from 'node:os';

import type { LoggerFacade } from '../logging/logger-facade.js';

/**
 * Layer-3 server-side pipe primitive (unbound state). Holds the
 * configuration needed to claim a named-pipe address and produces a
 * {@link BoundPipeListener} via {@link bind}. The OS resource is
 * created during {@link bind}; if the address is unavailable the
 * failure surfaces there.
 *
 * Type-state design: an unbound `PipeListener` has no server, so it
 * has no `dispose`; only the {@link BoundPipeListener} owns OS
 * resources. This rules out "run before bind" at compile time.
 *
 * Counterpart of the C# `PipeListener` in `AutoContext.Framework`.
 */
export class PipeListener {
    private readonly pipeName: string;
    private readonly logger: LoggerFacade;
    private bound = false;

    constructor(pipeName: string, logger: LoggerFacade) {
        if (pipeName.length === 0) {
            throw new TypeError('pipeName must be non-empty.');
        }
        this.pipeName = pipeName;
        this.logger = logger;
    }

    /**
     * Claims the pipe address by listening on the underlying
     * `net.Server`. One-shot \u2014 subsequent calls reject. Recovers
     * from a stale POSIX socket inode left by a prior crash.
     */
    async bind(): Promise<BoundPipeListener> {
        if (this.bound) {
            throw new Error(`Pipe listener for '${this.pipeName}' has already been bound.`);
        }
        this.bound = true;

        const path = PipeListener.resolvePath(this.pipeName);
        const server = createServer();
        try {
            await PipeListener.listenWithStaleRecovery(server, path);
        } catch (ex) {
            await new Promise<void>((resolve) => {
                server.close(() => resolve());
            });
            throw ex;
        }

        return new BoundPipeListener(this.pipeName, path, server, this.logger);
    }

    /**
     * Resolves a logical pipe name to the platform-specific path used
     * by `net.Server.listen`. Mirrors `PipeTransport.resolvePath`.
     */
    private static resolvePath(pipeName: string): string {
        if (platform() === 'win32') {
            return pipeName.startsWith('\\\\') ? pipeName : `\\\\.\\pipe\\${pipeName}`;
        }
        return pipeName.startsWith('/') ? pipeName : `/tmp/CoreFxPipe_${pipeName}`;
    }

    private static async listenWithStaleRecovery(server: Server, path: string): Promise<void> {
        try {
            await PipeListener.listenOnce(server, path);
            return;
        } catch (ex) {
            const err = ex as NodeJS.ErrnoException;
            if (process.platform === 'win32' || err.code !== 'EADDRINUSE' || !path.startsWith('/')) {
                throw ex;
            }
            const isStale = await PipeListener.probeStaleSocket(path);
            if (!isStale) {
                throw ex;
            }
            try {
                unlinkSync(path);
            } catch {
                throw ex;
            }
            await PipeListener.listenOnce(server, path);
        }
    }

    private static listenOnce(server: Server, path: string): Promise<void> {
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
            server.listen(path);
        });
    }

    private static probeStaleSocket(path: string): Promise<boolean> {
        return new Promise<boolean>((resolve) => {
            const socket = connect(path);
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
}

/**
 * Layer-3 server-side pipe primitive (bound state). Owns the
 * `net.Server` and runs an accept loop, dispatching each accepted
 * socket to the caller-supplied handler. Only producible via
 * {@link PipeListener.bind}.
 *
 * Each accepted {@link Socket} is owned by the listener and destroyed
 * after the handler returns; handlers should not call `socket.end()`
 * or `socket.destroy()` themselves.
 *
 * {@link run} is one-shot. {@link dispose} is the canonical teardown
 * and may be called whether or not {@link run} ran.
 */
export class BoundPipeListener {
    private readonly pipeName: string;
    private readonly path: string;
    private readonly server: Server;
    private readonly logger: LoggerFacade;
    private readonly sockets = new Set<Socket>();
    private readonly inFlight = new Set<Promise<void>>();
    private running = false;
    private disposed = false;
    private disposePromise: Promise<void> | undefined;

    /** @internal Use {@link PipeListener.bind}. */
    constructor(pipeName: string, path: string, server: Server, logger: LoggerFacade) {
        this.pipeName = pipeName;
        this.path = path;
        this.server = server;
        this.logger = logger;
    }

    /**
     * Runs the accept loop. Returns only after {@link signal} aborts
     * AND every in-flight handler has finished.
     *
     * @param handler Invoked once per accepted connection with the
     *     socket and a signal that fires on caller abort or listener
     *     dispose. The listener destroys the socket after the handler
     *     resolves; the handler must not destroy it.
     * @param signal Cooperative cancellation. The accept loop closes
     *     when it aborts and outstanding handlers drain before the
     *     promise resolves.
     */
    async run(
        handler: (socket: Socket, signal: AbortSignal) => Promise<void>,
        signal: AbortSignal,
    ): Promise<void> {
        if (this.disposed) {
            throw new Error(`Pipe listener for '${this.pipeName}' has been disposed.`);
        }
        if (this.running) {
            throw new Error(`Pipe listener for '${this.pipeName}' has already been run.`);
        }
        this.running = true;

        const stopController = new AbortController();
        const linkedSignal = AbortSignal.any([signal, stopController.signal]);

        const onConnection = (socket: Socket): void => {
            this.sockets.add(socket);
            socket.once('close', () => this.sockets.delete(socket));
            const task = this.invokeHandler(socket, handler, linkedSignal);
            this.inFlight.add(task);
            void task.finally(() => this.inFlight.delete(task));
        };

        this.server.on('connection', onConnection);

        try {
            if (signal.aborted) {
                return;
            }
            await new Promise<void>((resolve) => {
                signal.addEventListener('abort', () => resolve(), { once: true });
            });
        } finally {
            this.server.removeListener('connection', onConnection);
            stopController.abort();
            await this.shutdown();
        }
    }

    /**
     * Closes the underlying server and destroys any sockets still
     * connected. Safe to call multiple times; concurrent callers share
     * the same completion promise.
     */
    async dispose(): Promise<void> {
        if (this.disposePromise !== undefined) {
            return this.disposePromise;
        }
        this.disposed = true;
        this.disposePromise = this.shutdown();
        return this.disposePromise;
    }

    private async shutdown(): Promise<void> {
        for (const socket of this.sockets) {
            socket.destroy();
        }
        this.sockets.clear();

        await new Promise<void>((resolve) => {
            this.server.close(() => resolve());
        });

        await Promise.allSettled(this.inFlight);
    }

    private async invokeHandler(
        socket: Socket,
        handler: (socket: Socket, signal: AbortSignal) => Promise<void>,
        signal: AbortSignal,
    ): Promise<void> {
        try {
            await handler(socket, signal);
        } catch (ex) {
            const message = ex instanceof Error ? `${ex.name}: ${ex.message}` : String(ex);
            this.logger.error(
                `Pipe listener '${this.pipeName}' connection handler threw an unhandled exception: ${message}`,
                ex,
            );
        } finally {
            socket.end();
            socket.destroy();
        }
    }

    /** @internal Path the underlying server is bound to. Useful for diagnostics/tests. */
    get listenPath(): string {
        return this.path;
    }
}
