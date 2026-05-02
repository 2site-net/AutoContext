import * as vscode from 'vscode';
import { type Socket } from 'node:net';
import { BoundPipeListener, LengthPrefixedFrameCodec, PipeListener } from 'autocontext-framework-web';
import { IdentifierFactory } from './identifier-factory.js';
import type { ServerEntry } from './server-entry.js';
import type { WorkerManager } from './worker-manager.js';
import type { ChannelLogger } from 'autocontext-framework-web';

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
 * see `LengthPrefixedFrameCodec` in `AutoContext.Framework.Transport` and
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
    private readonly pipeName: string;
    private readonly idToIdentity = new Map<string, string>();
    private readonly stopController = new AbortController();
    private bound: BoundPipeListener | undefined;

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
     * @param logger      ChannelLogger for diagnostic output. Failures here
     *                    are best-effort — the server must not crash
     *                    the host on bad input.
     */
    constructor(
        private readonly workerManager: WorkerManager,
        entries: readonly ServerEntry[],
        instanceId: string,
        private readonly logger: ChannelLogger,
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
    async start(): Promise<void> {
        if (this.bound !== undefined) {
            return;
        }

        const listener = new PipeListener(this.pipeName, this.logger);
        this.bound = await listener.bind();
        this.logger.info(`Listening on pipe: ${this.pipeName}`);

        void this.bound.run(
            (socket, signal) => this.handleConnection(socket, signal),
            this.stopController.signal,
        );
    }

    dispose(): void {
        if (!this.stopController.signal.aborted) {
            this.stopController.abort();
        }
        if (this.bound !== undefined) {
            void this.bound.dispose();
            this.bound = undefined;
        }
    }

    private async handleConnection(socket: Socket, signal: AbortSignal): Promise<void> {
        const channel = new LengthPrefixedFrameCodec(socket);

        // Persistent multi-request loop. The orchestrator opens one
        // socket and sends multiple `ensureRunning` requests over its
        // lifetime, so we keep reading until the peer closes or the
        // listener is shutting down.
        try {
            while (!signal.aborted) {
                const payload = await channel.read(signal);
                if (payload === null) {
                    return;
                }
                await this.dispatch(channel, payload, signal);
            }
        }
        catch (err) {
            if (signal.aborted) { return; }
            this.logger.warn('Control connection terminated unexpectedly', err);
        }
    }

    private async dispatch(channel: LengthPrefixedFrameCodec, payload: Buffer, signal: AbortSignal): Promise<void> {
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
            await this.send(channel, {
                status: 'failed',
                error: `No worker registered with id '${request.workerId}'.`,
            }, signal);
            return;
        }

        try {
            await this.workerManager.ensureRunning(identity);
            await this.send(channel, { status: 'ready' }, signal);
        }
        catch (err) {
            const message = err instanceof Error ? err.message : String(err);
            await this.send(channel, { status: 'failed', error: message }, signal);
        }
    }

    private async send(channel: LengthPrefixedFrameCodec, response: EnsureRunningResponse, signal: AbortSignal): Promise<void> {
        const payload = Buffer.from(JSON.stringify(response), 'utf8');
        try {
            await channel.write(payload, signal);
        }
        catch (err) {
            if (signal.aborted) { return; }
            this.logger.warn('Failed to write control response', err);
        }
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
