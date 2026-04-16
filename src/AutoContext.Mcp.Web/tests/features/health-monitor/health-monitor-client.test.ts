import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { createServer, type Server } from 'node:net';
import { HealthMonitorClient } from '../../../src/features/health-monitor/health-monitor-client.js';
import { NullLogger, type Logger } from '../../../src/features/logging/logger.js';

function pipePath(pipeName: string): string {
    return process.platform === 'win32'
        ? `\\\\.\\pipe\\${pipeName}`
        : `/tmp/CoreFxPipe_${pipeName}`;
}

/** Wait for a condition to become true (or timeout). */
async function waitFor(condition: () => boolean, ms = 2000, interval = 10): Promise<void> {
    const start = Date.now();
    while (!condition()) {
        if (Date.now() - start > ms) { throw new Error('waitFor timeout'); }
        await new Promise(r => setTimeout(r, interval));
    }
}

describe('HealthMonitorClient', () => {
    let testPipe: string;
    let server: Server;
    let receivedData: string;
    let connectionCount: number;

    beforeEach(() => {
        testPipe = `test-health-monitor-${process.pid}-${Date.now()}`;
        receivedData = '';
        connectionCount = 0;

        server = createServer((socket) => {
            connectionCount++;
            socket.on('data', (data) => {
                receivedData += data.toString('utf8');
            });
        });

        server.listen(pipePath(testPipe));
    });

    afterEach(async () => {
        await new Promise<void>(resolve => server.close(() => resolve()));
    });

    it('should connect and send category name', async () => {
        const client = new HealthMonitorClient(NullLogger);
        client.connect(testPipe, 'typescript');

        await waitFor(() => receivedData.length > 0);

        expect(receivedData).toBe('typescript');
        expect(connectionCount).toBe(1);

        client.dispose();
    });

    it('should log on successful connection', async () => {
        const logger: Logger = { log: vi.fn() };
        const client = new HealthMonitorClient(logger);
        client.connect(testPipe, 'dotnet');

        await waitFor(() => (logger.log as ReturnType<typeof vi.fn>).mock.calls.length > 0);

        expect(logger.log).toHaveBeenCalledWith('Startup', `health-monitor=${testPipe}`);

        client.dispose();
    });

    it('should log on connection failure', async () => {
        const logger: Logger = { log: vi.fn() };
        const client = new HealthMonitorClient(logger);
        client.connect('nonexistent-pipe-name', 'dotnet');

        await waitFor(() => (logger.log as ReturnType<typeof vi.fn>).mock.calls.length > 0);

        expect(logger.log).toHaveBeenCalledWith('Startup', 'health-monitor connection failed');

        client.dispose();
    });

    it('should destroy socket on dispose', async () => {
        const client = new HealthMonitorClient(NullLogger);
        client.connect(testPipe, 'git');

        await waitFor(() => connectionCount === 1);

        client.dispose();
        // Calling dispose again should be safe (no-op)
        client.dispose();
    });
});
