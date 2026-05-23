import { describe, it, expect } from 'vitest';
import { PipeStreamingClient } from '#src/pipes/pipe-streaming-client.js';
import { PipeTransport } from '#src/pipes/pipe-transport.js';
import { PipeListener } from '#src/pipes/pipe-listener.js';
import { FakeLoggerFactory } from '../support/logging/fake-logger-factory.js';
import { PipeNameTestFactory } from '../support/pipes/pipe-name-test-factory.js';
import { PromiseTestUtils } from '../support/shared/promise-test-utils.js';

interface CapturedEcho {
    readonly captured: Buffer[];
    stop: () => Promise<void>;
}

async function startCapturingEcho(pipeName: string): Promise<CapturedEcho> {
    const captured: Buffer[] = [];
    const bound = await new PipeListener(pipeName, FakeLoggerFactory.create()).bind();
    const ac = new AbortController();
    const runTask = bound.run(async (socket) => {
        socket.on('data', (chunk: Buffer) => captured.push(chunk));
        await new Promise<void>((resolve) => socket.once('close', () => resolve()));
    }, ac.signal);
    return {
        captured,
        stop: async () => {
            ac.abort();
            await runTask;
            await bound.dispose();
        },
    };
}

describe('PipeStreamingClient', () => {
    it('writes serialized items over the pipe', async () => {
        const name = PipeNameTestFactory.create();
        const echo = await startCapturingEcho(name);
        const client = new PipeStreamingClient<string>({
            transport: new PipeTransport(FakeLoggerFactory.create()),
            pipeName: name,
            serialize: (s) => Buffer.from(s, 'utf8'),
            logger: FakeLoggerFactory.create(),
        });
        try {
            client.post('hello');
            client.post('world');
            await PromiseTestUtils.until(() => Buffer.concat(echo.captured).toString('utf8') === 'helloworld');
        }
        finally {
            await client.dispose();
            await echo.stop();
        }
    });

    it('writes the greeting before any items', async () => {
        const name = PipeNameTestFactory.create();
        const echo = await startCapturingEcho(name);
        const client = new PipeStreamingClient<string>({
            transport: new PipeTransport(FakeLoggerFactory.create()),
            pipeName: name,
            serialize: (s) => Buffer.from(s, 'utf8'),
            logger: FakeLoggerFactory.create(),
            greeting: Buffer.from('GREET\n', 'utf8'),
        });
        try {
            client.post('A');
            await PromiseTestUtils.until(() => Buffer.concat(echo.captured).toString('utf8') === 'GREET\nA');
        }
        finally {
            await client.dispose();
            await echo.stop();
        }
    });

    it('routes items to the fallback when the pipe name is empty', async () => {
        const fallback: string[] = [];
        const client = new PipeStreamingClient<string>({
            transport: new PipeTransport(FakeLoggerFactory.create()),
            pipeName: '',
            serialize: (s) => Buffer.from(s, 'utf8'),
            logger: FakeLoggerFactory.create(),
            fallback: (item) => fallback.push(item),
        });
        try {
            client.post('a');
            client.post('b');
            await PromiseTestUtils.until(() => fallback.length === 2);
            expect(fallback).toEqual(['a', 'b']);
        }
        finally {
            await client.dispose();
        }
    });

    it('drops the oldest item when the queue overflows', async () => {
        const fallback: string[] = [];
        const client = new PipeStreamingClient<string>({
            transport: new PipeTransport(FakeLoggerFactory.create()),
            pipeName: '',
            serialize: (s) => Buffer.from(s, 'utf8'),
            logger: FakeLoggerFactory.create(),
            fallback: (item) => fallback.push(item),
            queueCapacity: 2,
        });
        try {
            // Synchronous burst before the drain loop yields past its first await.
            client.post('a');
            client.post('b');
            client.post('c');
            await PromiseTestUtils.until(() => fallback.length === 2);
            expect(fallback).toEqual(['b', 'c']);
        }
        finally {
            await client.dispose();
        }
    });

    it('drops items posted after dispose', async () => {
        const fallback: string[] = [];
        const client = new PipeStreamingClient<string>({
            transport: new PipeTransport(FakeLoggerFactory.create()),
            pipeName: '',
            serialize: (s) => Buffer.from(s, 'utf8'),
            logger: FakeLoggerFactory.create(),
            fallback: (item) => fallback.push(item),
        });
        await client.dispose();
        client.post('lost');
        await new Promise<void>((resolve) => setTimeout(resolve, 20));
        expect(fallback).toEqual([]);
    });

    it('dispose is idempotent', async () => {
        const client = new PipeStreamingClient<string>({
            transport: new PipeTransport(FakeLoggerFactory.create()),
            pipeName: '',
            serialize: (s) => Buffer.from(s, 'utf8'),
            logger: FakeLoggerFactory.create(),
        });
        await expect(client.dispose()).resolves.toBeUndefined();
        await expect(client.dispose()).resolves.toBeUndefined();
        await expect(client.dispose()).resolves.toBeUndefined();
    });
});
