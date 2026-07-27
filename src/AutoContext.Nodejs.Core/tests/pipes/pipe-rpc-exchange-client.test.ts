import { describe, it, expect } from 'vitest';
import { createFakeLogger } from '../support/logging/fake-logger.js';
import { createTestPipeName } from 'autocontext-nodejs-tests-support';
import { LengthPrefixedFrameCodec } from '#src/pipes/length-prefixed-frame-codec.js';
import { PipeListener } from '#src/pipes/pipe-listener.js';
import { PipeRpcExchangeClient } from '#src/pipes/pipe-rpc-exchange-client.js';
import { PipeTransport } from '#src/pipes/pipe-transport.js';

interface RunningServer {
    stop: () => Promise<void>;
}

/**
 * Serves one framed connection with `handler`, which owns the read and
 * write loop for that connection.
 */
async function startServer(
    pipeName: string,
    handler: (codec: LengthPrefixedFrameCodec, signal: AbortSignal) => Promise<void>,
): Promise<RunningServer> {
    const bound = await new PipeListener(pipeName, createFakeLogger()).bind();
    const ac = new AbortController();
    const runTask = bound.run(async (socket, signal) => {
        await handler(new LengthPrefixedFrameCodec(socket), signal);
    }, ac.signal);

    return {
        stop: async () => {
            ac.abort();
            await runTask;
            await bound.dispose();
        },
    };
}

/** Answers every request frame with the request text uppercased. */
async function serveUppercasingEcho(
    codec: LengthPrefixedFrameCodec,
    signal: AbortSignal,
): Promise<void> {
    try {
        for (;;) {
            const request = await codec.read(signal);
            if (request === null) {
                return;
            }
            await codec.write(Buffer.from(request.toString('utf8').toUpperCase(), 'utf8'), signal);
        }
    }
    catch {
        // The connection tore down; the accept loop reclaims the socket.
    }
}

function createClient(pipeName: string): PipeRpcExchangeClient {
    return new PipeRpcExchangeClient({
        transport: new PipeTransport(createFakeLogger()),
        pipeName,
        logger: createFakeLogger(),
    });
}

describe('PipeRpcExchangeClient', () => {
    it('resolves each exchange with the frame the peer answered', async () => {
        const name = createTestPipeName();
        const server = await startServer(name, serveUppercasingEcho);
        const client = createClient(name);
        try {
            const response = await client.exchange(Buffer.from('ping', 'utf8'));
            expect(response.toString('utf8')).toBe('PING');
        }
        finally {
            await client.dispose();
            await server.stop();
        }
    });

    it('pairs concurrent exchanges with their own responses', async () => {
        const name = createTestPipeName();
        const server = await startServer(name, serveUppercasingEcho);
        const client = createClient(name);
        try {
            const requests = ['alpha', 'beta', 'gamma', 'delta'];
            const responses = await Promise.all(
                requests.map((text) => client.exchange(Buffer.from(text, 'utf8'))));

            expect(responses.map((r) => r.toString('utf8')))
                .toEqual(requests.map((text) => text.toUpperCase()));
        }
        finally {
            await client.dispose();
            await server.stop();
        }
    });

    it('rejects when the peer closes before answering', async () => {
        const name = createTestPipeName();
        const server = await startServer(name, async (codec, signal) => {
            await codec.read(signal);
        });
        const client = createClient(name);
        try {
            await expect(client.exchange(Buffer.from('ping', 'utf8')))
                .rejects.toThrow(/before answering/);
        }
        finally {
            await client.dispose();
            await server.stop();
        }
    });

    it('rejects every later exchange once the connection has faulted', async () => {
        const name = createTestPipeName();
        const server = await startServer(name, async (codec, signal) => {
            await codec.read(signal);
        });
        const client = createClient(name);
        try {
            await expect(client.exchange(Buffer.from('first', 'utf8'))).rejects.toThrow();
            await expect(client.exchange(Buffer.from('second', 'utf8')))
                .rejects.toThrow(/before answering/);
        }
        finally {
            await client.dispose();
            await server.stop();
        }
    });

    it('rejects an exchange issued after dispose', async () => {
        const name = createTestPipeName();
        const server = await startServer(name, serveUppercasingEcho);
        const client = createClient(name);
        try {
            await client.exchange(Buffer.from('ping', 'utf8'));
            await client.dispose();

            await expect(client.exchange(Buffer.from('ping', 'utf8')))
                .rejects.toThrow(/disposed/);
        }
        finally {
            await server.stop();
        }
    });

    it('rejects an empty pipe name', () => {
        expect(() => new PipeRpcExchangeClient({
            transport: new PipeTransport(createFakeLogger()),
            pipeName: '',
            logger: createFakeLogger(),
        })).toThrow(/pipeName/);
    });
});
