import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { connect, type Socket } from 'node:net';
import { WorkerControlServer } from '#src/worker-control-server';
import { ServerEntry } from '#src/server-entry';
import type { WorkerManager } from '#src/worker-manager';
import { createFakeLogger } from '#testing/fakes';
import { pipePath } from '#testing/utils/pipe-helpers';
import { waitFor } from '#testing/utils/wait-for';

const HEADER_BYTES = 4;

const SUFFIX = 'wctest' + Math.random().toString(16).slice(2, 10);

const entries: readonly ServerEntry[] = [
    new ServerEntry('workspace', 'AutoContext.Worker.Workspace', 'dotnet'),
    new ServerEntry('dotnet', 'AutoContext.Worker.DotNet', 'dotnet'),
    new ServerEntry('web', 'AutoContext.Worker.Web', 'node'),
];

function frame(payload: object): Buffer {
    const json = Buffer.from(JSON.stringify(payload), 'utf8');
    const message = Buffer.alloc(HEADER_BYTES + json.length);
    message.writeInt32LE(json.length, 0);
    json.copy(message, HEADER_BYTES);
    return message;
}

async function connectClient(pipeName: string): Promise<Socket> {
    return new Promise((resolve, reject) => {
        const socket = connect(pipePath(pipeName), () => resolve(socket));
        socket.once('error', reject);
    });
}

async function readOneResponse(socket: Socket): Promise<unknown> {
    return new Promise((resolve, reject) => {
        let buffer = Buffer.alloc(0);
        const onData = (chunk: Buffer): void => {
            buffer = Buffer.concat([buffer, chunk]);
            if (buffer.length < HEADER_BYTES) { return; }
            const length = buffer.readInt32LE(0);
            if (buffer.length < HEADER_BYTES + length) { return; }
            const payload = buffer.subarray(HEADER_BYTES, HEADER_BYTES + length);
            socket.off('data', onData);
            try {
                resolve(JSON.parse(payload.toString('utf8')));
            }
            catch (err) {
                reject(err);
            }
        };
        socket.on('data', onData);
        socket.once('error', reject);
    });
}

describe('WorkerControlServer', () => {
    let server: WorkerControlServer;
    let workerManager: WorkerManager;
    let ensureRunning: ReturnType<typeof vi.fn>;
    const logger = createFakeLogger();

    beforeEach(() => {
        vi.clearAllMocks();
        ensureRunning = vi.fn().mockResolvedValue(undefined);
        workerManager = { ensureRunning } as unknown as WorkerManager;
        server = new WorkerControlServer(workerManager, entries, SUFFIX, logger);
        server.start();
    });

    afterEach(() => {
        server.dispose();
    });

    it('exposes a deterministic pipe name keyed off the suffix', () => {
        expect(server.getPipeName()).toBe(`autocontext.worker-control-${SUFFIX}`);
    });

    it('rejects construction when endpoint suffix is empty', () => {
        expect(() => new WorkerControlServer(workerManager, entries, '', logger))
            .toThrow(/non-empty endpoint suffix/);
    });

    it('responds with status: ready and forwards the slot identity to ensureRunning', async () => {
        const socket = await connectClient(server.getPipeName());
        socket.write(frame({ type: 'ensureRunning', workerId: 'workspace' }));

        const response = await readOneResponse(socket);

        expect(response).toEqual({ status: 'ready' });
        expect(ensureRunning).toHaveBeenCalledExactlyOnceWith('Worker.Workspace');

        socket.destroy();
    });

    it('translates an unknown worker id to status: failed without calling ensureRunning', async () => {
        const socket = await connectClient(server.getPipeName());
        socket.write(frame({ type: 'ensureRunning', workerId: 'bogus' }));

        const response = (await readOneResponse(socket)) as { status: string; error?: string };

        expect(response.status).toBe('failed');
        expect(response.error).toMatch(/bogus/);
        expect(ensureRunning).not.toHaveBeenCalled();

        socket.destroy();
    });

    it('translates an ensureRunning rejection to status: failed with the error message', async () => {
        ensureRunning.mockRejectedValueOnce(new Error('spawn ENOENT'));
        const socket = await connectClient(server.getPipeName());
        socket.write(frame({ type: 'ensureRunning', workerId: 'dotnet' }));

        const response = (await readOneResponse(socket)) as { status: string; error?: string };

        expect(response).toEqual({ status: 'failed', error: 'spawn ENOENT' });

        socket.destroy();
    });

    it('handles multiple sequential requests on a persistent connection', async () => {
        const socket = await connectClient(server.getPipeName());
        socket.write(frame({ type: 'ensureRunning', workerId: 'workspace' }));
        const first = await readOneResponse(socket);
        socket.write(frame({ type: 'ensureRunning', workerId: 'dotnet' }));
        const second = await readOneResponse(socket);

        expect(first).toEqual({ status: 'ready' });
        expect(second).toEqual({ status: 'ready' });
        expect(ensureRunning).toHaveBeenCalledTimes(2);
        expect(ensureRunning).toHaveBeenNthCalledWith(1, 'Worker.Workspace');
        expect(ensureRunning).toHaveBeenNthCalledWith(2, 'Worker.DotNet');

        socket.destroy();
    });

    it('drops the connection when a malformed length is received', async () => {
        const socket = await connectClient(server.getPipeName());
        const closed = new Promise<void>(resolve => socket.once('close', () => resolve()));

        // 32-bit two's-complement -1 is rejected as a malformed length.
        const bad = Buffer.alloc(HEADER_BYTES);
        bad.writeInt32LE(-1, 0);
        socket.write(bad);

        await closed;
        expect(ensureRunning).not.toHaveBeenCalled();
    });

    it('drops malformed JSON without sending a response and keeps the connection alive', async () => {
        const socket = await connectClient(server.getPipeName());

        // Send a length-framed payload that is not valid JSON, then a
        // valid request after it. The server must ignore the first
        // (no response) and answer the second.
        const garbage = Buffer.from('not-json{{', 'utf8');
        const garbageHeader = Buffer.alloc(HEADER_BYTES);
        garbageHeader.writeInt32LE(garbage.length, 0);
        socket.write(Buffer.concat([garbageHeader, garbage]));
        socket.write(frame({ type: 'ensureRunning', workerId: 'workspace' }));

        const response = await readOneResponse(socket);

        expect(response).toEqual({ status: 'ready' });
        expect(ensureRunning).toHaveBeenCalledExactlyOnceWith('Worker.Workspace');

        socket.destroy();
    });

    it('drops requests with an unknown type without responding', async () => {
        const socket = await connectClient(server.getPipeName());
        socket.write(frame({ type: 'unknownThing', workerId: 'workspace' }));

        // Race a small delay against an unexpected response — when the
        // delay wins (no data) the server correctly ignored the request.
        const got = await Promise.race([
            readOneResponse(socket).then(r => ({ kind: 'response' as const, r })),
            new Promise<{ kind: 'timeout' }>(resolve =>
                setTimeout(() => resolve({ kind: 'timeout' }), 100)),
        ]);

        expect(got.kind).toBe('timeout');
        expect(ensureRunning).not.toHaveBeenCalled();

        socket.destroy();
    });

    it('cleans up open sockets when disposed', async () => {
        const socket = await connectClient(server.getPipeName());
        const closed = new Promise<void>(resolve => socket.once('close', () => resolve()));

        server.dispose();

        await closed;
        await waitFor(() => socket.destroyed === true);
    });
});
