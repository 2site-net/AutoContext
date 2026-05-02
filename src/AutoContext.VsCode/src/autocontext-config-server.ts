import * as vscode from 'vscode';
import { type Socket } from 'node:net';
import { BoundPipeListener, LengthPrefixedFrameCodec, PipeListener } from 'autocontext-framework-web';
import { IdentifierFactory } from './identifier-factory.js';
import type { AutoContextConfigManager } from './autocontext-config-manager.js';
import type { McpToolsDisabledSnapshot } from '#types/mcp-tools-disabled-snapshot.js';
import type { ChannelLogger } from 'autocontext-framework-web';

/**
 * Named-pipe server that broadcasts the current `disabledTools` /
 * `disabledTasks` projection of `.autocontext.json` to in-process
 * subscribers (currently only `AutoContext.Mcp.Server`). The
 * extension is the single writer of `.autocontext.json`; subscribers
 * are read-only consumers that learn about disabled-state changes
 * via this push channel rather than re-implementing file watching,
 * debouncing, schema parsing, or kebab/camel conversion.
 *
 * Wire protocol per message: 4-byte little-endian payload length,
 * then that many UTF-8 JSON bytes — same framing as
 * {@link WorkerControlServer}. Push-based:
 *
 * - On every successful client connection the server immediately
 *   sends the current snapshot (handshake = first frame).
 * - On every {@link AutoContextConfigManager.onDidChange} the
 *   server re-broadcasts the new snapshot to every live subscriber.
 *
 * Snapshots are full and idempotent — subscribers replace their
 * local state on each frame, so reconnects and replays are safe by
 * construction.
 */
export class AutoContextConfigServer implements vscode.Disposable {
    private readonly pipeName: string;
    private readonly subscribers = new Set<Socket>();
    private readonly disposables: vscode.Disposable[] = [];
    private readonly stopController = new AbortController();
    private bound: BoundPipeListener | undefined;

    constructor(
        private readonly configManager: AutoContextConfigManager,
        instanceId: string,
        private readonly logger: ChannelLogger,
    ) {
        this.pipeName = IdentifierFactory.createServiceAddress('extension-config', instanceId);
        this.disposables.push(
            configManager.onDidChange(() => {
                void this.broadcastCurrent().catch(err =>
                    this.logger.error('Failed to broadcast config snapshot', err),
                );
            }),
        );
    }

    /** Pipe name subscribers should connect to. */
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
        for (const d of this.disposables) {
            d.dispose();
        }
        this.disposables.length = 0;

        if (!this.stopController.signal.aborted) {
            this.stopController.abort();
        }
        if (this.bound !== undefined) {
            void this.bound.dispose();
            this.bound = undefined;
        }
        this.subscribers.clear();
    }

    private async handleConnection(socket: Socket, signal: AbortSignal): Promise<void> {
        this.subscribers.add(socket);
        // Subscribers are read-only; drop anything they send. We still
        // attach a 'data' handler so Node's default behaviour (buffering
        // forever) doesn't accumulate memory if a misbehaving client
        // writes to us.
        socket.on('data', () => { /* discard inbound */ });

        try {
            // Push the current snapshot immediately as the handshake frame.
            await this.sendSnapshot(socket, signal);

            // Hold the connection open until the peer disconnects or
            // the listener stops; broadcastCurrent() pushes further
            // frames in the meantime.
            await new Promise<void>((resolve) => {
                const finish = (): void => {
                    signal.removeEventListener('abort', finish);
                    resolve();
                };
                socket.once('close', finish);
                socket.once('error', finish);
                signal.addEventListener('abort', finish, { once: true });
            });
        }
        finally {
            this.subscribers.delete(socket);
        }
    }

    private async sendSnapshot(socket: Socket, signal: AbortSignal): Promise<void> {
        if (socket.destroyed) {
            return;
        }
        const config = await this.configManager.read();
        await AutoContextConfigServer.write(socket, config.getToolsDisabledSnapshot(), signal);
    }

    private async broadcastCurrent(): Promise<void> {
        if (this.subscribers.size === 0) {
            return;
        }
        const config = await this.configManager.read();
        const snapshot = config.getToolsDisabledSnapshot();
        const signal = this.stopController.signal;
        for (const socket of this.subscribers) {
            try {
                await AutoContextConfigServer.write(socket, snapshot, signal);
            }
            catch (err) {
                if (signal.aborted) { return; }
                this.logger.warn('Failed to push config snapshot to subscriber', err);
            }
        }
    }

    private static async write(socket: Socket, snapshot: McpToolsDisabledSnapshot, signal: AbortSignal): Promise<void> {
        if (socket.destroyed) {
            return;
        }
        const codec = new LengthPrefixedFrameCodec(socket);
        await codec.write(Buffer.from(JSON.stringify(snapshot), 'utf8'), signal);
    }
}
