import { describe, it, expect, afterEach } from 'vitest';
import { createServer, type Server, type Socket } from 'node:net';
import { randomUUID } from 'node:crypto';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { HealthMonitorClient } from '#src/hosting/health-monitor-client.js';
import { NullLogger } from '#src/logging/null-logger.js';

function makePipeName(): string {
    if (process.platform === 'win32') {
        return `\\\\.\\pipe\\actx-health-test-${randomUUID()}`;
    }
    return join(tmpdir(), `actx-health-test-${randomUUID()}.sock`);
}

interface PipeServer {
    server: Server;
    awaitConnection: () => Promise<Socket>;
    received: () => Buffer;
    close: () => Promise<void>;
}

function startServer(pipePath: string): Promise<PipeServer> {
    return new Promise((resolve, reject) => {
        let connectedSocket: Socket | undefined;
        let connectionResolver: ((socket: Socket) => void) | undefined;
        const buffers: Buffer[] = [];

        const server = createServer((socket) => {
            connectedSocket = socket;
            socket.on('data', (chunk: Buffer) => buffers.push(chunk));
            connectionResolver?.(socket);
        });

        server.once('error', reject);
        server.listen(pipePath, () => {
            resolve({
                server,
                awaitConnection: () => new Promise<Socket>((res) => {
                    if (connectedSocket !== undefined) {
                        res(connectedSocket);
                        return;
                    }
                    connectionResolver = res;
                }),
                received: () => Buffer.concat(buffers),
                close: () => new Promise<void>((res) => {
                    connectedSocket?.destroy();
                    server.close(() => res());
                }),
            });
        });
    });
}

async function waitFor<T>(predicate: () => T | undefined, timeoutMs = 2000): Promise<T> {
    const start = Date.now();
    while (true) {
        const value = predicate();
        if (value !== undefined && value !== null) {
            return value;
        }
        if (Date.now() - start > timeoutMs) {
            throw new Error(`waitFor timed out after ${timeoutMs}ms`);
        }
        await new Promise((r) => setTimeout(r, 10));
    }
}

describe('HealthMonitorClient', () => {
    const clients: HealthMonitorClient[] = [];
    const servers: PipeServer[] = [];

    afterEach(async () => {
        for (const c of clients) {
            await c.dispose();
        }
        for (const s of servers) {
            await s.close();
        }
        clients.length = 0;
        servers.length = 0;
    });

    it('throws when workerId is blank', () => {
        expect(() => new HealthMonitorClient('p', '   ', NullLogger.instance)).toThrow(/workerId/);
    });

    it('writes the worker id and keeps the socket open', async () => {
        const pipePath = makePipeName();
        const ps = await startServer(pipePath);
        servers.push(ps);

        const client = new HealthMonitorClient(pipePath, 'web', NullLogger.instance);
        clients.push(client);

        await client.start();
        const socket = await ps.awaitConnection();

        const written = await waitFor(() => {
            const buf = ps.received();
            return buf.length > 0 ? buf : undefined;
        });

        expect(written.toString('utf8')).toBe('web');
        expect(socket.destroyed).toBe(false);
    });

    it('disconnects on dispose', async () => {
        const pipePath = makePipeName();
        const ps = await startServer(pipePath);
        servers.push(ps);

        const client = new HealthMonitorClient(pipePath, 'web', NullLogger.instance);
        await client.start();
        const socket = await ps.awaitConnection();

        await waitFor(() => (ps.received().length > 0 ? true : undefined));

        await client.dispose();

        // The server-side socket should observe the close.
        await waitFor(() => (socket.destroyed || (socket as { readableEnded?: boolean }).readableEnded ? true : undefined));
        expect(socket.destroyed || (socket as { readableEnded?: boolean }).readableEnded).toBe(true);
    });

    it('is a no-op when the pipe name is empty', async () => {
        const client = new HealthMonitorClient('', 'web', NullLogger.instance);
        clients.push(client);

        // start() should resolve without throwing.
        await client.start();
        await client.dispose();
    });

    it('does not throw when no server is listening', async () => {
        const pipePath = makePipeName();
        const client = new HealthMonitorClient(pipePath, 'web', NullLogger.instance);
        clients.push(client);

        // start() swallows the connect failure (best-effort signal).
        await client.start();
        await client.dispose();
    });
});
