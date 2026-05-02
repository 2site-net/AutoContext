import { PipeKeepAliveClient, PipeTransport } from 'autocontext-framework-web';

import type { LoggerFacade } from 'autocontext-framework-web';

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
 * Composed over {@link PipeKeepAliveClient}. This type owns the
 * worker-id handshake and the diagnostic logging context; the
 * keep-alive primitive owns connect, write-once, and socket lifetime.
 *
 * TypeScript counterpart of `HealthMonitorClient` in
 * `AutoContext.Framework`.
 */
export class HealthMonitorClient {
    private readonly pipeName: string;
    private readonly workerId: string;
    private readonly logger: LoggerFacade;
    private readonly keepAlive: PipeKeepAliveClient;

    constructor(pipeName: string, workerId: string, logger: LoggerFacade) {
        if (workerId.trim() === '') {
            throw new Error('workerId must be a non-empty string.');
        }
        this.pipeName = pipeName;
        this.workerId = workerId;
        this.logger = logger;
        this.keepAlive = new PipeKeepAliveClient(new PipeTransport(logger), logger);
    }

    /**
     * Connects to the health-monitor pipe and writes the worker id.
     * Resolves once the id has been flushed (or immediately when no
     * pipe was supplied). The socket is intentionally kept open
     * afterwards. Safe to call multiple times.
     */
    async start(): Promise<void> {
        const handshake = Buffer.from(this.workerId, 'utf8');
        await this.keepAlive.start(this.pipeName, handshake);
        if (this.pipeName !== '') {
            this.logger.debug(
                `Connected to health-monitor pipe '${this.pipeName}' as worker '${this.workerId}'.`);
        }
    }

    /**
     * Closes the socket so the extension observes the disconnect and
     * marks the worker as no longer running. Safe to call multiple
     * times.
     */
    dispose(): Promise<void> {
        return this.keepAlive.dispose();
    }
}
