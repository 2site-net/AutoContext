import * as vscode from 'vscode';
import { createServer, type Server, type Socket } from 'node:net';
import { IdentifierFactory } from './identifier-factory.js';
import type { Logger } from '#types/logger.js';

/**
 * Monitors MCP worker health via a named pipe.
 *
 * Each worker connects to the pipe on startup and sends its worker id
 * (e.g. "dotnet", "web", "workspace") as a UTF-8 string. The connection
 * is kept alive for the lifetime of the worker; when its process exits
 * the socket closes and the monitor updates the health status.
 *
 * NOTE: this surface is in a transitional state — `Mcp.Server` ignores
 * the `--health-monitor` argument and the workers don't yet connect, so
 * the monitor reports nothing as running at runtime. The wiring will be
 * redesigned later; the API exposed here is the minimum the tree
 * provider needs.
 */
export class HealthMonitorServer implements vscode.Disposable {
    private readonly _onDidChange = new vscode.EventEmitter<void>();
    readonly onDidChange = this._onDidChange.event;

    private readonly pipeName = IdentifierFactory.createRandomName('autocontext-health');
    private readonly connections = new Map<string, Set<Socket>>();
    private server: Server | undefined;

    constructor(
        private readonly logger: Logger,
    ) {}

    /**
     * The pipe name workers should connect to.
     */
    getPipeName(): string {
        return this.pipeName;
    }

    /**
     * Returns `true` if at least one connection is active for the given worker id.
     */
    isRunning(workerId: string): boolean {
        const sockets = this.connections.get(workerId);
        return sockets !== undefined && sockets.size > 0;
    }

    /**
     * Starts the named pipe server.
     */
    start(): void {
        const pipePath = process.platform === 'win32'
            ? `\\\\.\\pipe\\${this.pipeName}`
            : `/tmp/CoreFxPipe_${this.pipeName}`;

        this.server = createServer((socket: Socket) => {
            let workerId = '';

            socket.on('data', (data: Buffer) => {
                // The worker sends its id as the first (and only) message.
                if (!workerId) {
                    workerId = data.toString('utf8').trim();
                    this.addConnection(workerId, socket);
                }
            });

            socket.on('close', () => {
                if (workerId) {
                    this.removeConnection(workerId, socket);
                }
            });

            socket.on('error', () => {
                if (workerId) {
                    this.removeConnection(workerId, socket);
                }
            });
        });

        this.server.on('error', (err) => {
            this.logger.error('Pipe server error', err);
        });

        this.server.listen(pipePath, () => {
            this.logger.info(`Listening on pipe: ${this.pipeName}`);
        });
    }

    private addConnection(workerId: string, socket: Socket): void {
        let sockets = this.connections.get(workerId);
        if (!sockets) {
            sockets = new Set();
            this.connections.set(workerId, sockets);
        }

        const wasEmpty = sockets.size === 0;
        sockets.add(socket);

        if (wasEmpty) {
            this.logger.info(`${workerId}: connected`);
            this._onDidChange.fire();
        }
    }

    private removeConnection(workerId: string, socket: Socket): void {
        const sockets = this.connections.get(workerId);
        if (!sockets) { return; }

        sockets.delete(socket);

        if (sockets.size === 0) {
            this.logger.info(`${workerId}: disconnected`);
            this._onDidChange.fire();
        }
    }

    dispose(): void {
        this._onDidChange.dispose();

        if (this.server) {
            this.server.close();
            this.server = undefined;
        }

        for (const sockets of this.connections.values()) {
            for (const socket of sockets) {
                socket.destroy();
            }
        }
        this.connections.clear();
    }
}
