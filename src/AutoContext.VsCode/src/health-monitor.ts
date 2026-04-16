import * as vscode from 'vscode';
import { createServer, type Server, type Socket } from 'node:net';
import { randomUUID } from 'node:crypto';
import { groupServerCategories } from './ui-constants.js';

/**
 * Monitors MCP server health via a named pipe.
 *
 * Each MCP server connects to the pipe on startup and sends its category
 * name (e.g. "dotnet", "typescript", "git", "editorconfig") as a UTF-8
 * string.  The connection is kept alive for the lifetime of the server.
 * When the server process exits, the socket closes, and the monitor
 * updates the health status accordingly.
 */
export class HealthMonitorServer implements vscode.Disposable {
    private static readonly groupCategories = groupServerCategories;

    private readonly _onDidChange = new vscode.EventEmitter<void>();
    readonly onDidChange = this._onDidChange.event;

    private readonly pipeName = `autocontext-health-${randomUUID().replace(/-/g, '').slice(0, 12)}`;
    private readonly connections = new Map<string, Set<Socket>>();
    private server: Server | undefined;

    constructor(private readonly outputChannel: vscode.OutputChannel) {}

    /**
     * The pipe name MCP servers should connect to.
     */
    getPipeName(): string {
        return this.pipeName;
    }

    /**
     * Returns `true` if at least one connection is active for the given category.
     */
    isRunning(category: string): boolean {
        const sockets = this.connections.get(category);
        return sockets !== undefined && sockets.size > 0;
    }

    /**
     * Returns `true` if all categories in the group have at least one active connection.
     */
    isGroupHealthy(group: string): boolean {
        const categories = HealthMonitorServer.groupCategories.get(group);
        if (!categories) { return false; }
        return categories.every(c => this.isRunning(c));
    }

    /**
     * Returns `true` if at least one category in the group has an active connection.
     */
    isGroupPartiallyHealthy(group: string): boolean {
        const categories = HealthMonitorServer.groupCategories.get(group);
        if (!categories) { return false; }
        return categories.some(c => this.isRunning(c));
    }

    /**
     * Starts the named pipe server.
     */
    start(): void {
        const pipePath = process.platform === 'win32'
            ? `\\\\.\\pipe\\${this.pipeName}`
            : `/tmp/CoreFxPipe_${this.pipeName}`;

        this.server = createServer((socket: Socket) => {
            let category = '';

            socket.on('data', (data: Buffer) => {
                // The server sends its category name as the first (and only) message.
                if (!category) {
                    category = data.toString('utf8').trim();
                    this.addConnection(category, socket);
                }
            });

            socket.on('close', () => {
                if (category) {
                    this.removeConnection(category, socket);
                }
            });

            socket.on('error', () => {
                if (category) {
                    this.removeConnection(category, socket);
                }
            });
        });

        this.server.on('error', (err) => {
            this.outputChannel.appendLine(`[HealthMonitor] Pipe server error: ${err.message}`);
        });

        this.server.listen(pipePath, () => {
            this.outputChannel.appendLine(`[HealthMonitor] Listening on pipe: ${this.pipeName}`);
        });
    }

    private addConnection(category: string, socket: Socket): void {
        let sockets = this.connections.get(category);
        if (!sockets) {
            sockets = new Set();
            this.connections.set(category, sockets);
        }

        const wasEmpty = sockets.size === 0;
        sockets.add(socket);

        if (wasEmpty) {
            this.outputChannel.appendLine(`[HealthMonitor] ${category}: connected`);
            this._onDidChange.fire();
        }
    }

    private removeConnection(category: string, socket: Socket): void {
        const sockets = this.connections.get(category);
        if (!sockets) { return; }

        sockets.delete(socket);

        if (sockets.size === 0) {
            this.outputChannel.appendLine(`[HealthMonitor] ${category}: disconnected`);
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
