import { describe, it, expect } from 'vitest';
import { createFakeLogger } from '../support/logging/fake-logger.js';
import { createTestPipeName, waitFor } from 'autocontext-nodejs-tests-support';
import { LengthPrefixedFrameCodec } from '#src/pipes/length-prefixed-frame-codec.js';
import { PipeEventsSubscriptionClient } from '#src/pipes/pipe-events-subscription-client.js';
import { PipeListener } from '#src/pipes/pipe-listener.js';
import { PipeTransport } from '#src/pipes/pipe-transport.js';

interface RunningServer {
    readonly received: Buffer[];
    stop: () => Promise<void>;
}

/**
 * Records the frames a subscriber sends, then pushes `pushed` back and
 * holds the connection open until `keepOpen` resolves.
 */
async function startPublisher(
    pipeName: string,
    pushed: readonly string[],
    keepOpen?: Promise<void>,
): Promise<RunningServer> {
    const received: Buffer[] = [];
    const bound = await new PipeListener(pipeName, createFakeLogger()).bind();
    const ac = new AbortController();
    const runTask = bound.run(async (socket, signal) => {
        const codec = new LengthPrefixedFrameCodec(socket);
        try {
            const subscribe = await codec.read(signal);
            if (subscribe !== null) {
                received.push(subscribe);
            }
            for (const frame of pushed) {
                await codec.write(Buffer.from(frame, 'utf8'), signal);
            }
            if (keepOpen !== undefined) {
                await keepOpen;
            }
        }
        catch {
            // The connection tore down; the accept loop reclaims the socket.
        }
    }, ac.signal);

    return {
        received,
        stop: async () => {
            ac.abort();
            await runTask;
            await bound.dispose();
        },
    };
}

function createClient(pipeName: string): PipeEventsSubscriptionClient {
    return new PipeEventsSubscriptionClient({
        transport: new PipeTransport(createFakeLogger()),
        pipeName,
        logger: createFakeLogger(),
    });
}

describe('PipeEventsSubscriptionClient', () => {
    it('sends the subscribe frames before yielding anything', async () => {
        const name = createTestPipeName();
        const server = await startPublisher(name, ['first']);
        const client = createClient(name);
        try {
            const frames: string[] = [];
            for await (const frame of client.subscribe([Buffer.from('SUBSCRIBE', 'utf8')])) {
                frames.push(frame.toString('utf8'));
            }

            expect(server.received.map((r) => r.toString('utf8'))).toEqual(['SUBSCRIBE']);
            expect(frames).toEqual(['first']);
        }
        finally {
            await client.dispose();
            await server.stop();
        }
    });

    it('yields pushed frames in order and ends when the peer closes', async () => {
        const name = createTestPipeName();
        const server = await startPublisher(name, ['one', 'two', 'three']);
        const client = createClient(name);
        try {
            const frames: string[] = [];
            for await (const frame of client.subscribe([Buffer.from('SUBSCRIBE', 'utf8')])) {
                frames.push(frame.toString('utf8'));
            }

            expect(frames).toEqual(['one', 'two', 'three']);
        }
        finally {
            await client.dispose();
            await server.stop();
        }
    });

    it('ends an in-flight subscription when disposed', async () => {
        const name = createTestPipeName();
        let release = (): void => { /* replaced below */ };
        const keepOpen = new Promise<void>((resolve) => { release = resolve; });
        const server = await startPublisher(name, ['one'], keepOpen);
        const client = createClient(name);
        try {
            const frames: string[] = [];
            const stream = client.subscribe([Buffer.from('SUBSCRIBE', 'utf8')]);
            const drain = (async () => {
                for await (const frame of stream) {
                    frames.push(frame.toString('utf8'));
                }
            })();

            await waitFor(() => frames.length === 1);
            await client.dispose();
            await drain;

            expect(frames).toEqual(['one']);
        }
        finally {
            release();
            await client.dispose();
            await server.stop();
        }
    });

    it('rejects a second subscription on the same client', async () => {
        const name = createTestPipeName();
        let release = (): void => { /* replaced below */ };
        const keepOpen = new Promise<void>((resolve) => { release = resolve; });
        const server = await startPublisher(name, ['one'], keepOpen);
        const client = createClient(name);
        try {
            const first = client.subscribe([Buffer.from('SUBSCRIBE', 'utf8')]);
            await first.next();

            const second = client.subscribe([Buffer.from('SUBSCRIBE', 'utf8')]);
            await expect(second.next()).rejects.toThrow(/already subscribed/);

            await first.return(undefined);
        }
        finally {
            release();
            await client.dispose();
            await server.stop();
        }
    });

    it('rejects a subscription issued after dispose', async () => {
        const name = createTestPipeName();
        const server = await startPublisher(name, []);
        const client = createClient(name);
        try {
            await client.dispose();

            const stream = client.subscribe([]);
            await expect(stream.next()).rejects.toThrow(/disposed/);
        }
        finally {
            await server.stop();
        }
    });

    it('rejects an empty pipe name', () => {
        expect(() => new PipeEventsSubscriptionClient({
            transport: new PipeTransport(createFakeLogger()),
            pipeName: '',
            logger: createFakeLogger(),
        })).toThrow(/pipeName/);
    });
});
