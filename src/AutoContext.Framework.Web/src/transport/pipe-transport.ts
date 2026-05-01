import { connect, type NetConnectOpts, type Socket } from 'node:net';
import { platform } from 'node:os';

import type { Logger } from '../logging/logger.js';

/**
 * Layer 1 transport primitive for AutoContext named-pipe communication
 * on Node. Provides a uniform {@link PipeTransport.connect} entry point
 * so endpoint clients (logging, health-monitor, worker request /
 * response) do not each have to call `net.connect` and assemble the
 * platform-specific path by hand.
 *
 * Phase 1 scope: client-side connect only. The server-side accept-loop
 * primitive will be introduced when the first server endpoint is
 * migrated (Phase 4 in the unification plan).
 *
 * Counterpart of the C# `PipeTransport` in `AutoContext.Framework`.
 */
export class PipeTransport {
    private readonly logger: Logger;

    /**
     * Creates a new {@link PipeTransport}. The {@link logger} is
     * mandatory; pass a `NullLogger` for silent operation.
     */
    constructor(logger: Logger) {
        this.logger = logger;
    }

    /**
     * Opens a connection to the named pipe identified by
     * {@link pipeName} on the local machine.
     *
     * @param pipeName The pipe name (without the `\\.\pipe\` prefix
     *     on Windows). Must be non-empty.
     * @param signal Optional cancellation signal. The returned promise
     *     rejects with an `AbortError` if the signal fires before the
     *     connection completes.
     */
    async connect(pipeName: string, signal?: AbortSignal): Promise<Socket> {
        if (pipeName.length === 0) {
            throw new TypeError('pipeName must be non-empty.');
        }

        signal?.throwIfAborted();

        const path = PipeTransport.resolvePath(pipeName);
        const options: NetConnectOpts = { path };

        return new Promise<Socket>((resolve, reject) => {
            const socket = connect(options);

            let settled = false;
            let abortListener: (() => void) | undefined;

            const settle = (action: () => void): void => {
                if (settled) {
                    return;
                }
                settled = true;
                socket.removeListener('connect', onConnect);
                socket.removeListener('error', onError);
                if (abortListener !== undefined && signal !== undefined) {
                    signal.removeEventListener('abort', abortListener);
                }
                action();
            };

            const onConnect = (): void => {
                this.logger.debug(`PipeTransport connected to '${pipeName}'.`);
                settle(() => resolve(socket));
            };

            const onError = (err: Error): void => {
                settle(() => {
                    socket.destroy();
                    reject(err);
                });
            };

            socket.once('connect', onConnect);
            socket.once('error', onError);

            if (signal !== undefined) {
                abortListener = (): void => {
                    settle(() => {
                        socket.destroy();
                        const reason: unknown = signal.reason;
                        reject(reason instanceof Error ? reason : Object.assign(new Error('The operation was aborted.'), { name: 'AbortError' }));
                    });
                };
                signal.addEventListener('abort', abortListener, { once: true });
            }
        });
    }

    /**
     * Resolves a logical pipe name to the platform-specific path used
     * by `net.connect`: a Windows named pipe under `\\.\pipe\` or the
     * `/tmp/CoreFxPipe_*` socket emitted by .NET `NamedPipeServerStream`
     * on POSIX. Already-rooted paths (`\\…` on Windows, `/…` on POSIX)
     * are passed through unchanged so callers retain full control.
     */
    private static resolvePath(pipeName: string): string {
        if (platform() === 'win32') {
            return pipeName.startsWith('\\\\') ? pipeName : `\\\\.\\pipe\\${pipeName}`;
        }

        return pipeName.startsWith('/') ? pipeName : `/tmp/CoreFxPipe_${pipeName}`;
    }
}
