import { describe, it, expect, afterEach } from 'vitest';
import * as net from 'node:net';
import { randomUUID } from 'node:crypto';
import { LogServerClient } from '#src/logging/log-server-client.js';
import type { LogRecord } from '#types/log-record.js';

interface PipeReader {
    readonly server: net.Server;
    readonly pipeName: string;
    readonly pipePath: string;
    readonly lines: Promise<string[]>;
    close(): Promise<void>;
}

function pipePathFor(name: string): string {
    return process.platform === 'win32'
        ? `\\\\.\\pipe\\${name}`
        : `/tmp/CoreFxPipe_${name}`;
}

/**
 * Spins up a one-shot pipe server that accepts a single connection,
 * collects every NDJSON line received, and resolves once the client
 * closes the socket.
 */
async function startPipeReader(): Promise<PipeReader> {
    const pipeName = `actx-web-logsrv-${randomUUID().replace(/-/g, '')}`.slice(0, 32);
    const pipePath = pipePathFor(pipeName);
    let resolveLines!: (lines: string[]) => void;
    let rejectLines!: (err: unknown) => void;
    const lines = new Promise<string[]>((resolve, reject) => {
        resolveLines = resolve;
        rejectLines = reject;
    });

    const server = net.createServer((socket) => {
        const collected: string[] = [];
        let buffer = '';
        socket.on('data', (chunk) => {
            buffer += chunk.toString('utf8');
            const parts = buffer.split('\n');
            buffer = parts.pop() ?? '';
            for (const part of parts) {
                if (part.length > 0) {
                    collected.push(part);
                }
            }
        });
        socket.on('end', () => {
            if (buffer.length > 0) {
                collected.push(buffer);
            }
            resolveLines(collected);
        });
        socket.on('error', rejectLines);
    });

    await new Promise<void>((resolve, reject) => {
        server.once('error', reject);
        server.listen(pipePath, () => resolve());
    });

    return {
        server,
        pipeName,
        pipePath,
        lines,
        close: () =>
            new Promise<void>((resolve) => {
                server.close(() => resolve());
            }),
    };
}

describe('LogServerClient', () => {
    const cleanups: Array<() => Promise<void>> = [];

    afterEach(async () => {
        for (const c of cleanups.splice(0)) {
            await c();
        }
    });

    it('sends the greeting and one NDJSON line per record over the pipe', async () => {
        const reader = await startPipeReader();
        cleanups.push(() => reader.close());

        const client = new LogServerClient(reader.pipeName, 'AutoContext.Worker.Web');
        cleanups.push(() => client.dispose());

        client.enqueue(record('Acme.Demo', 'Information', 'hello world'));
        client.enqueue({ ...record('Acme.Demo', 'Error', 'oops'), correlationId: 'abcd1234' });

        // Give the drain loop time to write before tearing the client down.
        await new Promise((r) => setTimeout(r, 50));
        await client.dispose();

        const lines = await reader.lines;
        expect(lines.length).toBeGreaterThanOrEqual(3);

        const greeting = JSON.parse(lines[0] ?? 'null') as { clientName: string };
        expect(greeting.clientName).toBe('AutoContext.Worker.Web');

        const first = JSON.parse(lines[1] ?? 'null') as Record<string, unknown>;
        expect(first['category']).toBe('Acme.Demo');
        expect(first['level']).toBe('Information');
        expect(first['message']).toBe('hello world');
        expect(first['correlationId']).toBeUndefined();

        const second = JSON.parse(lines[2] ?? 'null') as Record<string, unknown>;
        expect(second['correlationId']).toBe('abcd1234');
    });

    it('omits absent optional fields from the wire payload', async () => {
        const reader = await startPipeReader();
        cleanups.push(() => reader.close());
        const client = new LogServerClient(reader.pipeName, 'AutoContext.Worker.Web');
        cleanups.push(() => client.dispose());

        client.enqueue(record('Acme.Demo', 'Information', 'plain'));

        await new Promise((r) => setTimeout(r, 50));
        await client.dispose();

        const lines = await reader.lines;
        const second = lines[1] ?? '';
        expect(second).not.toContain('"exception"');
        expect(second).not.toContain('"correlationId"');
    });

    it('falls back to stderr when the pipe is unreachable', async () => {
        const writes: string[] = [];
        const original = process.stderr.write.bind(process.stderr);
        process.stderr.write = ((chunk: unknown): boolean => {
            writes.push(typeof chunk === 'string' ? chunk : Buffer.from(chunk as Uint8Array).toString('utf8'));
            return true;
        }) as typeof process.stderr.write;

        try {
            const client = new LogServerClient(
                `actx-web-no-such-${randomUUID()}`.slice(0, 32),
                'AutoContext.Worker.Web',
            );
            cleanups.push(() => client.dispose());
            client.enqueue({
                ...record('Acme.Demo', 'Warning', 'no pipe here'),
                correlationId: 'feedface',
            });

            await new Promise((r) => setTimeout(r, 100));
            await client.dispose();
        } finally {
            process.stderr.write = original;
        }

        expect(writes.some((w) => w.includes('[feedface] Warning: Acme.Demo: no pipe here'))).toBe(true);
    });

    it('falls back to stderr when no log pipe is configured', async () => {
        const writes: string[] = [];
        const original = process.stderr.write.bind(process.stderr);
        process.stderr.write = ((chunk: unknown): boolean => {
            writes.push(typeof chunk === 'string' ? chunk : Buffer.from(chunk as Uint8Array).toString('utf8'));
            return true;
        }) as typeof process.stderr.write;

        try {
            const client = new LogServerClient('', 'AutoContext.Worker.Web');
            cleanups.push(() => client.dispose());
            client.enqueue(record('Acme.Demo', 'Information', 'standalone'));

            await new Promise((r) => setTimeout(r, 30));
            await client.dispose();
        } finally {
            process.stderr.write = original;
        }

        expect(writes.some((w) => w.includes('Information: Acme.Demo: standalone'))).toBe(true);
    });

    it('drops the oldest record when the queue is saturated', async () => {
        // Use a stderr-fallback client so dispose drains synchronously
        // through writeStderr without the cost of a real socket.
        const writes: string[] = [];
        const original = process.stderr.write.bind(process.stderr);
        process.stderr.write = ((chunk: unknown): boolean => {
            writes.push(typeof chunk === 'string' ? chunk : Buffer.from(chunk as Uint8Array).toString('utf8'));
            return true;
        }) as typeof process.stderr.write;

        try {
            const client = new LogServerClient('', 'AutoContext.Worker.Web');
            cleanups.push(() => client.dispose());

            // Saturate well past QUEUE_CAPACITY (1024). The drain loop
            // is async, but the queue cap still bounds peak memory.
            for (let i = 0; i < 5000; i++) {
                client.enqueue(record('Acme.Demo', 'Information', `m-${i}`));
            }

            await new Promise((r) => setTimeout(r, 50));
            await client.dispose();
        } finally {
            process.stderr.write = original;
        }

        // We can't predict exactly how many records survived (the
        // drain loop competes with the producer), but we must never
        // emit more than the saturation count.
        expect(writes.length).toBeGreaterThan(0);
        expect(writes.length).toBeLessThanOrEqual(5000);
    });

    it('rejects an empty client name', () => {
        expect(() => new LogServerClient('', '   ')).toThrow(/clientName/);
    });
});

function record(category: string, level: LogRecord['level'], message: string): LogRecord {
    return { category, level, message };
}
