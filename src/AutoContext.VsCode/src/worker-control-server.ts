import * as vscode from 'vscode';
import { createServer, type Server, type Socket } from 'node:net';
import { IdentifierFactory } from './identifier-factory.js';
import type { ServerEntry } from './server-entry.js';
import type { WorkerManager } from './worker-manager.js';
import type { Logger } from '#types/logger.js';

/**
 * Wire shape of an `EnsureRunning` request received over the
 * worker-control pipe. `type` is fixed at `"ensureRunning"` so future
 * request kinds (idle-shutdown, runtime stats, …) can extend the
 * protocol without breaking back-compat. `workerId` is the **short**
 * id from `mcp-workers-registry.json` (e.g. `workspace`, `dotnet`,
 * `web`) — the same id that already flows through pipe naming and
 * health-monitor identity, so the orchestrator does not need to know
 * the extension-side slot key.
 */
interface EnsureRunningRequest {
    readonly type: 'ensureRunning';
    readonly workerId: string;
}

/**
 * Wire shape of an `EnsureRunning` response. `status` is always
 * present; `error` carries the failure message when, and only when,
 * `status === "failed"`.
 */
interface EnsureRunningResponse {
    readonly status: 'ready' | 'failed';
    readonly error?: string;
}

/**
 * Named-pipe server the orchestrator (`AutoContext.Mcp.Server`) talks
 * to before every worker pipe attempt to make sure the target worker
 * is alive. The extension owns worker lifecycle (see
 * {@link WorkerManager}); this server is the inbound edge that lets a
 * sibling host process trigger a spawn without owning child
 * processes itself.
 *
 * Wire protocol per message: 4-byte little-endian payload length,
 * then that many UTF-8 JSON bytes. Mirrors the worker task pipes —
 * see `WorkerProtocolChannel` in `AutoContext.Framework.Workers` and
 * its TypeScript counterpart in `AutoContext.Worker.Web`. Connections
 * are persistent: the orchestrator opens one socket and reuses it
 * for every subsequent `ensureRunning` round trip.
 *
 * Failure modes are intentionally narrow: spawn failures surface as a
 * `status: "failed"` response with the underlying error message.
 * Malformed or unknown-type requests are dropped with a warning log
 * — the orchestrator's wait deadline will surface the silence as a
 * pipe failure to the caller.
 */
export class WorkerControlServer implements vscode.Disposable {
    /** Length prefix size in bytes (32-bit little-endian unsigned). */
    private static readonly HeaderBytes = 4;
    /**
     * Maximum payload size accepted by the read loop. Caps allocation
     * when a corrupted or hostile header arrives; control messages are
     * tiny JSON envelopes well below this cap.
     */
    private static readonly MaxMessageBytes = 1 * 1024 * 1024;

    private readonly pipeName: string;
    private readonly idToIdentity = new Map<string, string>();
    private readonly sockets = new Set<Socket>();
    private server: Server | undefined;

    /**
     * @param workerManager  Lifecycle owner the server delegates spawn
     *                       requests to.
     * @param entries     Registered worker entries — used to resolve
     *                    a short id from the wire (e.g. `"workspace"`)
     *                    to the slot identity used by
     *                    {@link WorkerManager.ensureRunning}
     *                    (e.g. `"Worker.Workspace"`).
     * @param instanceId  Per-window identifier shared with
     *                    {@link WorkerManager}. Pipe address is
     *                    `autocontext.worker-control#<instance-id>`.
     * @param logger      Logger for diagnostic output. Failures here
     *                    are best-effort — the server must not crash
     *                    the host on bad input.
     */
    constructor(
        private readonly workerManager: WorkerManager,
        entries: readonly ServerEntry[],
        instanceId: string,
        private readonly logger: Logger,
    ) {
        this.pipeName = IdentifierFactory.createServiceAddress('worker-control', instanceId);
        for (const entry of entries) {
            this.idToIdentity.set(entry.id, entry.getShortName());
        }
    }

    /** Pipe name the orchestrator should connect to. */
    getPipeName(): string {
        return this.pipeName;
    }

    /** Starts the named-pipe accept loop. Idempotent. */
    start(): void {
        if (this.server) {
            return;
        }

        const pipePath = process.platform === 'win32'
            ? `\\\\.\\pipe\\${this.pipeName}`
            : `/tmp/CoreFxPipe_${this.pipeName}`;

        this.server = createServer((socket: Socket) => this.handleConnection(socket));

        this.server.on('error', (err) => {
            this.logger.error('Pipe server error', err);
        });

        this.server.listen(pipePath, () => {
            this.logger.info(`Listening on pipe: ${this.pipeName}`);
        });
    }

    dispose(): void {
        for (const socket of this.sockets) {
            socket.destroy();
        }
        this.sockets.clear();

        if (this.server) {
            this.server.close();
            this.server = undefined;
        }
    }

    private handleConnection(socket: Socket): void {
        this.sockets.add(socket);

        // Per-connection inbound buffer. We accumulate raw bytes from
        // 'data' and pop framed messages off the front whenever a full
        // header + payload is available — keeps the read path
        // back-pressure-friendly without taking on a heavyweight stream
        // adapter for what is, on the wire, two field-typed JSON shapes.
        let buffer: Buffer = Buffer.alloc(0);

        socket.on('data', (chunk: Buffer) => {
            buffer = buffer.length === 0 ? Buffer.from(chunk) : Buffer.concat([buffer, chunk]);

            while (buffer.length >= WorkerControlServer.HeaderBytes) {
                const length = buffer.readInt32LE(0);

                if (length < 0 || length > WorkerControlServer.MaxMessageBytes) {
                    this.logger.warn(`Dropping connection: malformed length ${length}`);
                    socket.destroy();
                    return;
                }

                if (buffer.length < WorkerControlServer.HeaderBytes + length) {
                    return; // wait for the rest of the payload
                }

                const payload = buffer.subarray(
                    WorkerControlServer.HeaderBytes,
                    WorkerControlServer.HeaderBytes + length,
                );
                buffer = buffer.subarray(WorkerControlServer.HeaderBytes + length);

                void this.dispatch(socket, payload);
            }
        });

        const cleanup = (): void => {
            this.sockets.delete(socket);
        };

        socket.on('close', cleanup);
        socket.on('error', cleanup);
    }

    private async dispatch(socket: Socket, payload: Buffer): Promise<void> {
        let request: unknown;
        try {
            request = JSON.parse(payload.toString('utf8'));
        }
        catch (err) {
            this.logger.warn('Dropping malformed control request', err);
            return;
        }

        if (!WorkerControlServer.isEnsureRunningRequest(request)) {
            this.logger.warn(`Dropping unknown control request: ${payload.toString('utf8')}`);
            return;
        }

        const identity = this.idToIdentity.get(request.workerId);
        if (!identity) {
            this.send(socket, {
                status: 'failed',
                error: `No worker registered with id '${request.workerId}'.`,
            });
            return;
        }

        try {
            await this.workerManager.ensureRunning(identity);
            this.send(socket, { status: 'ready' });
        }
        catch (err) {
            const message = err instanceof Error ? err.message : String(err);
            this.send(socket, { status: 'failed', error: message });
        }
    }

    private send(socket: Socket, response: EnsureRunningResponse): void {
        if (socket.destroyed) {
            return;
        }
        const payload = Buffer.from(JSON.stringify(response), 'utf8');
        const message = Buffer.allocUnsafe(WorkerControlServer.HeaderBytes + payload.length);
        message.writeInt32LE(payload.length, 0);
        payload.copy(message, WorkerControlServer.HeaderBytes);
        socket.write(message);
    }

    /**
     * Type-guard: a parsed payload is an `EnsureRunning` request iff
     * it carries `type === "ensureRunning"` and a non-empty
     * `workerId` string.
     */
    private static isEnsureRunningRequest(value: unknown): value is EnsureRunningRequest {
        if (typeof value !== 'object' || value === null) {
            return false;
        }
        const v = value as { type?: unknown; workerId?: unknown };
        return v.type === 'ensureRunning'
            && typeof v.workerId === 'string'
            && v.workerId.length > 0;
    }
}
