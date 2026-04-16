import { connect, type Socket } from 'node:net';
import type { Logger } from '../logging/logger.js';

/**
 * Keeps a persistent named-pipe connection to the extension's health
 * monitor.  The connection signals "I'm alive" for the given MCP server
 * category; when the process exits, the OS closes the socket and the
 * monitor detects the disconnect.
 */
export class HealthMonitorClient {
    private socket: Socket | undefined;

    constructor(private readonly logger: Logger) {}

    /**
     * Connects to the health monitor pipe and sends the category name.
     * The connection is kept alive until {@link dispose} is called or
     * the process exits.  Connection failures are logged but do not
     * throw — the health monitor is optional infrastructure.
     */
    connect(pipeName: string, category: string): void {
        const pipePath = process.platform === 'win32'
            ? `\\\\.\\pipe\\${pipeName}`
            : `/tmp/CoreFxPipe_${pipeName}`;

        const socket = connect(pipePath, () => {
            socket.write(category);
            this.logger.log('Startup', `health-monitor=${pipeName}`);
        });

        socket.on('error', () => {
            this.logger.log('Startup', 'health-monitor connection failed');
        });

        this.socket = socket;
    }

    dispose(): void {
        this.socket?.destroy();
        this.socket = undefined;
    }
}
