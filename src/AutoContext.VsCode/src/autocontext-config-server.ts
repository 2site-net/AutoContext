import * as vscode from 'vscode';
import { createServer, type Server, type Socket } from 'node:net';
import { IdentifierFactory } from './identifier-factory.js';
import type { AutoContextConfigManager } from './autocontext-config.js';
import { projectDisabledState, type DisabledStateSnapshot } from './config-context-projector.js';
import type { Logger } from '#types/logger.js';

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
    /** Length prefix size in bytes (32-bit little-endian unsigned). */
    private static readonly HeaderBytes = 4;

    private readonly pipeName: string;
    private readonly sockets = new Set<Socket>();
    private readonly disposables: vscode.Disposable[] = [];
    private server: Server | undefined;

    constructor(
        private readonly configManager: AutoContextConfigManager,
        instanceId: string,
        private readonly logger: Logger,
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
        for (const d of this.disposables) {
            d.dispose();
        }
        this.disposables.length = 0;

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

        const cleanup = (): void => {
            this.sockets.delete(socket);
        };
        socket.on('close', cleanup);
        socket.on('error', cleanup);
        // Subscribers are read-only; drop anything they send. We still
        // attach a 'data' handler so Node's default behaviour (buffering
        // forever) doesn't accumulate memory if a misbehaving client
        // writes to us.
        socket.on('data', () => { /* discard inbound */ });

        // Push the current snapshot immediately as the handshake frame.
        void this.sendSnapshot(socket).catch(err =>
            this.logger.error('Failed to send initial snapshot', err),
        );
    }

    private async sendSnapshot(socket: Socket): Promise<void> {
        if (socket.destroyed) {
            return;
        }
        const config = await this.configManager.read();
        AutoContextConfigServer.write(socket, projectDisabledState(config));
    }

    private async broadcastCurrent(): Promise<void> {
        if (this.sockets.size === 0) {
            return;
        }
        const config = await this.configManager.read();
        const snapshot = projectDisabledState(config);
        for (const socket of this.sockets) {
            AutoContextConfigServer.write(socket, snapshot);
        }
    }

    private static write(socket: Socket, snapshot: DisabledStateSnapshot): void {
        if (socket.destroyed) {
            return;
        }
        const payload = Buffer.from(JSON.stringify(snapshot), 'utf8');
        const message = Buffer.allocUnsafe(AutoContextConfigServer.HeaderBytes + payload.length);
        message.writeInt32LE(payload.length, 0);
        payload.copy(message, AutoContextConfigServer.HeaderBytes);
        socket.write(message);
    }
}
