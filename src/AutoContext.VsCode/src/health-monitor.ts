import * as vscode from 'vscode';
import { createServer, type Server, type Socket } from 'node:net';
import { randomUUID } from 'node:crypto';
import { serverLabelToScopesMap } from './ui-constants.js';

/**
 * Monitors MCP server health via a named pipe.
 *
 * Each MCP server connects to the pipe on startup and sends its scope
 * name (e.g. "dotnet", "typescript", "git", "editorconfig") as a UTF-8
 * string.  The connection is kept alive for the lifetime of the server.
 * When the server process exits, the socket closes, and the monitor
 * updates the health status accordingly.
 */
export class HealthMonitorServer implements vscode.Disposable {
    private static readonly serverLabelToScopesMap = serverLabelToScopesMap;

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
     * Returns `true` if at least one connection is active for the given scope.
     */
    isRunning(scope: string): boolean {
        const sockets = this.connections.get(scope);
        return sockets !== undefined && sockets.size > 0;
    }

    /**
     * Returns `true` if all scopes in the server label have at least one active connection.
     */
    isServerHealthy(serverLabel: string): boolean {
        const scopes = HealthMonitorServer.serverLabelToScopesMap.get(serverLabel);
        if (!scopes) { return false; }
        return scopes.every(c => this.isRunning(c));
    }

    /**
     * Returns `true` if at least one scope in the server label has an active connection.
     */
    isServerPartiallyHealthy(serverLabel: string): boolean {
        const scopes = HealthMonitorServer.serverLabelToScopesMap.get(serverLabel);
        if (!scopes) { return false; }
        return scopes.some(c => this.isRunning(c));
    }

    /**
     * Starts the named pipe server.
     */
    start(): void {
        const pipePath = process.platform === 'win32'
            ? `\\\\.\\pipe\\${this.pipeName}`
            : `/tmp/CoreFxPipe_${this.pipeName}`;

        this.server = createServer((socket: Socket) => {
            let scope = '';

            socket.on('data', (data: Buffer) => {
                // The server sends its scope name as the first (and only) message.
                if (!scope) {
                    scope = data.toString('utf8').trim();
                    this.addConnection(scope, socket);
                }
            });

            socket.on('close', () => {
                if (scope) {
                    this.removeConnection(scope, socket);
                }
            });

            socket.on('error', () => {
                if (scope) {
                    this.removeConnection(scope, socket);
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

    private addConnection(scope: string, socket: Socket): void {
        let sockets = this.connections.get(scope);
        if (!sockets) {
            sockets = new Set();
            this.connections.set(scope, sockets);
        }

        const wasEmpty = sockets.size === 0;
        sockets.add(socket);

        if (wasEmpty) {
            this.outputChannel.appendLine(`[HealthMonitor] ${scope}: connected`);
            this._onDidChange.fire();
        }
    }

    private removeConnection(scope: string, socket: Socket): void {
        const sockets = this.connections.get(scope);
        if (!sockets) { return; }

        sockets.delete(socket);

        if (sockets.size === 0) {
            this.outputChannel.appendLine(`[HealthMonitor] ${scope}: disconnected`);
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
