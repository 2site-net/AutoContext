import * as vscode from 'vscode';
import { type Socket } from 'node:net';
import { BoundPipeListener, PipeListener } from 'autocontext-framework-web';
import { IdentifierFactory } from './identifier-factory.js';
import type { ChannelLogger } from 'autocontext-framework-web';

/**
 * Monitors MCP worker and server health via a named pipe.
 *
 * Each managed child process (the MCP server and every worker)
 * connects to the pipe on startup and writes its client id
 * (`mcp-server`, `dotnet`, `web`, `workspace`) as a UTF-8 string. The
 * connection is kept alive for the lifetime of the host process; when
 * the process exits the OS closes the socket and the monitor flips
 * that client's status to "not running" and fires
 * {@link onDidChange}.
 *
 * The extension passes the pipe name to every spawned process via
 * `--health-monitor` (see {@link WorkerManager} for workers and
 * {@link McpServerProvider} for the MCP server); the client side is
 * the .NET `HealthMonitorClient` (in `AutoContext.Framework.Hosting`)
 * and its TypeScript counterpart in `AutoContext.Worker.Web`.
 */
export class HealthMonitorServer implements vscode.Disposable {
    private readonly _onDidChange = new vscode.EventEmitter<void>();
    readonly onDidChange = this._onDidChange.event;

    private readonly pipeName: string;
    private readonly connections = new Map<string, Set<Socket>>();
    private readonly stopController = new AbortController();
    private bound: BoundPipeListener | undefined;

    constructor(
        private readonly logger: ChannelLogger,
        instanceId: string,
    ) {
        this.pipeName = IdentifierFactory.createServiceAddress('health-monitor', instanceId);
    }

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

    private handleConnection(socket: Socket, signal: AbortSignal): Promise<void> {
        let workerId = '';

        socket.on('data', (data: Buffer) => {
            // The worker sends its id as the first (and only) message.
            // An empty or whitespace-only payload is treated as a
            // malformed handshake — we leave workerId blank so a later
            // well-formed write can still identify the connection, and
            // we never register an empty-string entry.
            if (!workerId) {
                const id = data.toString('utf8').trim();
                if (id.length === 0) { return; }
                workerId = id;
                this.addConnection(workerId, socket);
            }
        });

        return new Promise<void>((resolve) => {
            const finish = (): void => {
                signal.removeEventListener('abort', finish);
                if (workerId) {
                    this.removeConnection(workerId, socket);
                }
                resolve();
            };
            socket.once('close', finish);
            socket.once('error', finish);
            signal.addEventListener('abort', finish, { once: true });
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

        if (!this.stopController.signal.aborted) {
            this.stopController.abort();
        }
        if (this.bound !== undefined) {
            void this.bound.dispose();
            this.bound = undefined;
        }
        this.connections.clear();
    }
}
